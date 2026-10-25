using Bank.Domain;
using Bank.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bank.Infrastructure;

/// <summary>
/// The read store: denormalized rows ready to display. Reads come through <see cref="IAccountReadStore"/>;
/// only the projector (same assembly) can change it.
/// </summary>
internal sealed class ReadModelStore : IAccountReadStore
{
    private sealed class Row
    {
        public Guid Id;
        public string Owner = "";
        public decimal Balance;
        public decimal OverdraftLimit;
        public string Status = "Open";
        public readonly List<TransactionItem> Transactions = [];
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Row> _rows = [];

    public long LastSequence { get; private set; }

    public Task<AccountSummary?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_rows.TryGetValue(accountId, out var row) ? ToSummary(row) : null);
    }

    public Task<IReadOnlyList<TransactionItem>?> GetTransactionsAsync(
        Guid accountId, int skip, int take, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_rows.TryGetValue(accountId, out var row)) return Task.FromResult<IReadOnlyList<TransactionItem>?>(null);
            // Newest first, as a statement screen would show it.
            IReadOnlyList<TransactionItem> page = row.Transactions.AsEnumerable().Reverse().Skip(skip).Take(take).ToList();
            return Task.FromResult<IReadOnlyList<TransactionItem>?>(page);
        }
    }

    public Task<IReadOnlyList<AccountSummary>> ListAsync(string? owner, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AccountSummary> list = _rows.Values
                .Where(r => owner is null || string.Equals(r.Owner, owner, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Owner, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id)
                .Select(ToSummary).ToList();
            return Task.FromResult(list);
        }
    }

    public Task<IReadOnlyList<AccountSummary>> GetTopByBalanceAsync(int count, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<AccountSummary> top = _rows.Values
                .Where(r => r.Status == "Open")
                .OrderByDescending(r => r.Balance).ThenBy(r => r.Owner, StringComparer.OrdinalIgnoreCase)
                .Take(count).Select(ToSummary).ToList();
            return Task.FromResult(top);
        }
    }

    // ---- write access, reserved for the projector ----

    internal void Apply(LoggedEvent logged)
    {
        lock (_gate)
        {
            switch (logged.Event)
            {
                case AccountOpened e:
                    _rows[e.AccountId] = new Row { Id = e.AccountId, Owner = e.Owner, OverdraftLimit = e.OverdraftLimit };
                    break;
                case MoneyDeposited e when _rows.TryGetValue(e.AccountId, out var row):
                    row.Balance = e.BalanceAfter;
                    row.Transactions.Add(new TransactionItem(e.At, "Deposit", e.Amount, e.BalanceAfter));
                    break;
                case MoneyWithdrawn e when _rows.TryGetValue(e.AccountId, out var row):
                    row.Balance = e.BalanceAfter;
                    row.Transactions.Add(new TransactionItem(e.At, "Withdrawal", e.Amount, e.BalanceAfter));
                    break;
                case AccountClosed e when _rows.TryGetValue(e.AccountId, out var row):
                    row.Status = "Closed";
                    break;
            }
            LastSequence = logged.Sequence;
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _rows.Clear();
            LastSequence = 0;
        }
    }

    private static AccountSummary ToSummary(Row r) =>
        new(r.Id, r.Owner, r.Balance, r.OverdraftLimit, r.Status, r.Transactions.Count);
}

public interface IProjector
{
    /// <returns>How many events were applied to the read model.</returns>
    Task<int> RunPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Throws the read model away and rebuilds it from the whole event log.</summary>
    Task<int> RebuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps the read model up to date. It resumes from the last applied sequence, so running it twice
/// never applies an event twice. Between a command and the next run the read model is STALE:
/// that is eventual consistency.
/// </summary>
internal sealed class Projector(EventLog log, ReadModelStore store) : IProjector
{
    private readonly SemaphoreSlim _single = new(1, 1);

    public async Task<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        await _single.WaitAsync(cancellationToken);
        try
        {
            return Project();
        }
        finally
        {
            _single.Release();
        }
    }

    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        await _single.WaitAsync(cancellationToken);
        try
        {
            store.Clear();
            return Project();
        }
        finally
        {
            _single.Release();
        }
    }

    private int Project()
    {
        var pending = log.ReadAfter(store.LastSequence);
        foreach (var logged in pending) store.Apply(logged);
        return pending.Count;
    }
}

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EventLog>();
        services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
        services.AddSingleton<ReadModelStore>();
        services.AddSingleton<IAccountReadStore>(sp => sp.GetRequiredService<ReadModelStore>());
        services.AddSingleton<IProjector, Projector>();
        return services;
    }
}
