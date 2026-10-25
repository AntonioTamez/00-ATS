using Microsoft.Extensions.DependencyInjection;

namespace Bank.Queries;

public interface IQuery<TResult>;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

// The READ model: flat DTOs shaped for the screen. No behavior, no rules.
public sealed record AccountSummary(
    Guid Id, string Owner, decimal Balance, decimal OverdraftLimit, string Status, int TransactionCount);

public sealed record TransactionItem(DateTimeOffset At, string Type, decimal Amount, decimal BalanceAfter);

/// <summary>The only door the read side has to the data. Read-only by construction: every method is a question.</summary>
public interface IAccountReadStore
{
    Task<AccountSummary?> GetSummaryAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <returns>null when the account is not (yet) in the read model.</returns>
    Task<IReadOnlyList<TransactionItem>?> GetTransactionsAsync(Guid accountId, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummary>> ListAsync(string? owner, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummary>> GetTopByBalanceAsync(int count, CancellationToken cancellationToken = default);
}

public sealed record GetAccountQuery(Guid AccountId) : IQuery<AccountSummary?>;
public sealed record GetTransactionsQuery(Guid AccountId, int Skip = 0, int Take = 50) : IQuery<IReadOnlyList<TransactionItem>?>;
public sealed record ListAccountsQuery(string? Owner = null) : IQuery<IReadOnlyList<AccountSummary>>;
public sealed record GetRichestAccountsQuery(int Count = 3) : IQuery<IReadOnlyList<AccountSummary>>;

public sealed class GetAccountHandler(IAccountReadStore store) : IQueryHandler<GetAccountQuery, AccountSummary?>
{
    public Task<AccountSummary?> HandleAsync(GetAccountQuery query, CancellationToken cancellationToken = default) =>
        store.GetSummaryAsync(query.AccountId, cancellationToken);
}

public sealed class GetTransactionsHandler(IAccountReadStore store)
    : IQueryHandler<GetTransactionsQuery, IReadOnlyList<TransactionItem>?>
{
    public Task<IReadOnlyList<TransactionItem>?> HandleAsync(GetTransactionsQuery query, CancellationToken cancellationToken = default) =>
        store.GetTransactionsAsync(query.AccountId, Math.Max(0, query.Skip), Math.Clamp(query.Take, 1, 200), cancellationToken);
}

public sealed class ListAccountsHandler(IAccountReadStore store)
    : IQueryHandler<ListAccountsQuery, IReadOnlyList<AccountSummary>>
{
    public Task<IReadOnlyList<AccountSummary>> HandleAsync(ListAccountsQuery query, CancellationToken cancellationToken = default) =>
        store.ListAsync(string.IsNullOrWhiteSpace(query.Owner) ? null : query.Owner.Trim(), cancellationToken);
}

public sealed class GetRichestAccountsHandler(IAccountReadStore store)
    : IQueryHandler<GetRichestAccountsQuery, IReadOnlyList<AccountSummary>>
{
    public Task<IReadOnlyList<AccountSummary>> HandleAsync(GetRichestAccountsQuery query, CancellationToken cancellationToken = default) =>
        store.GetTopByBalanceAsync(Math.Clamp(query.Count, 1, 20), cancellationToken);
}

public static class QueriesRegistration
{
    /// <summary>Registers every IQueryHandler in this assembly.</summary>
    public static IServiceCollection AddQueries(this IServiceCollection services)
    {
        var handlerInterface = typeof(IQueryHandler<,>);

        foreach (var type in typeof(QueriesRegistration).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
                services.AddScoped(contract, type);

        return services;
    }
}
