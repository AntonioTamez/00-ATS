using Bank.Domain;

namespace Bank.Infrastructure;

public readonly record struct LoggedEvent(long Sequence, IDomainEvent Event);

/// <summary>Append-only log of everything that happened. The projector reads it to build the read model.</summary>
public sealed class EventLog
{
    private readonly object _gate = new();
    private readonly List<LoggedEvent> _events = [];

    public void Append(IEnumerable<IDomainEvent> events)
    {
        lock (_gate)
            foreach (var e in events)
                _events.Add(new LoggedEvent(_events.Count + 1, e));
    }

    public IReadOnlyList<LoggedEvent> ReadAfter(long sequence)
    {
        lock (_gate) return _events.Where(e => e.Sequence > sequence).ToList();
    }
}

/// <summary>
/// The write store. SaveAsync stores the state and appends its events under one lock,
/// the in-memory stand-in for "one transaction" (a real database would use an outbox table).
/// </summary>
public sealed class InMemoryAccountRepository(EventLog log) : IAccountRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Account> _accounts = [];

    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_accounts.GetValueOrDefault(id));
    }

    public Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _accounts[account.Id] = account;
            log.Append(account.PullEvents());
        }
        return Task.CompletedTask;
    }
}
