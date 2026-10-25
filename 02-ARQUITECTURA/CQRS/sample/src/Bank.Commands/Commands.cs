using Bank.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.Commands;

public interface ICommand<TResult>;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>A command that has nothing to give back. Commands return an id at most, never data to display.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

public sealed class NotFoundException(string message) : Exception(message);

// Commands express INTENT in business words, not "set Balance = x".
public sealed record OpenAccountCommand(string Owner, decimal OverdraftLimit) : ICommand<Guid>;
public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<Unit>;
public sealed record WithdrawCommand(Guid AccountId, decimal Amount) : ICommand<Unit>;
public sealed record CloseAccountCommand(Guid AccountId) : ICommand<Unit>;

public sealed class OpenAccountHandler(IAccountRepository accounts, TimeProvider clock)
    : ICommandHandler<OpenAccountCommand, Guid>
{
    public async Task<Guid> HandleAsync(OpenAccountCommand command, CancellationToken cancellationToken = default)
    {
        var account = Account.Open(command.Owner, command.OverdraftLimit, clock.GetUtcNow());
        await accounts.SaveAsync(account, cancellationToken);
        return account.Id;
    }
}

public sealed class DepositHandler(IAccountRepository accounts, TimeProvider clock)
    : ICommandHandler<DepositCommand, Unit>
{
    public async Task<Unit> HandleAsync(DepositCommand command, CancellationToken cancellationToken = default)
    {
        var account = await accounts.LoadAsync(command.AccountId, cancellationToken);
        account.Deposit(command.Amount, clock.GetUtcNow());
        await accounts.SaveAsync(account, cancellationToken);
        return Unit.Value;
    }
}

public sealed class WithdrawHandler(IAccountRepository accounts, TimeProvider clock)
    : ICommandHandler<WithdrawCommand, Unit>
{
    public async Task<Unit> HandleAsync(WithdrawCommand command, CancellationToken cancellationToken = default)
    {
        var account = await accounts.LoadAsync(command.AccountId, cancellationToken);
        account.Withdraw(command.Amount, clock.GetUtcNow());
        await accounts.SaveAsync(account, cancellationToken);
        return Unit.Value;
    }
}

public sealed class CloseAccountHandler(IAccountRepository accounts, TimeProvider clock)
    : ICommandHandler<CloseAccountCommand, Unit>
{
    public async Task<Unit> HandleAsync(CloseAccountCommand command, CancellationToken cancellationToken = default)
    {
        var account = await accounts.LoadAsync(command.AccountId, cancellationToken);
        account.Close(clock.GetUtcNow());
        await accounts.SaveAsync(account, cancellationToken);
        return Unit.Value;
    }
}

internal static class AccountRepositoryExtensions
{
    public static async Task<Account> LoadAsync(this IAccountRepository accounts, Guid id, CancellationToken ct) =>
        await accounts.FindAsync(id, ct) ?? throw new NotFoundException($"Account {id} was not found.");
}

public static class CommandsRegistration
{
    /// <summary>Registers every ICommandHandler in this assembly.</summary>
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        var handlerInterface = typeof(ICommandHandler<,>);

        foreach (var type in typeof(CommandsRegistration).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
                services.AddScoped(contract, type);

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
