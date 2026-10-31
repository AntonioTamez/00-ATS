using Bank.Commands;
using Bank.Infrastructure;
using Bank.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.Tests;

/// <summary>Both sides wired in memory, with a projector the test runs by hand (so staleness is observable).</summary>
internal sealed class Harness
{
    private readonly ServiceProvider _provider;

    public Harness()
    {
        _provider = new ServiceCollection()
            .AddCommands()
            .AddQueries()
            .AddInfrastructure()
            .BuildServiceProvider();
    }

    public IProjector Projector => _provider.GetRequiredService<IProjector>();
    public EventLog Log => _provider.GetRequiredService<EventLog>();

    public Task<TResult> Send<TCommand, TResult>(TCommand command) where TCommand : ICommand<TResult> =>
        _provider.CreateScope().ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().HandleAsync(command);

    public Task<TResult> Ask<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult> =>
        _provider.CreateScope().ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().HandleAsync(query);

    public Task<Guid> Open(string owner, decimal overdraft = 0m) =>
        Send<OpenAccountCommand, Guid>(new OpenAccountCommand(owner, overdraft));

    public Task Deposit(Guid id, decimal amount) => Send<DepositCommand, Unit>(new DepositCommand(id, amount));
    public Task Withdraw(Guid id, decimal amount) => Send<WithdrawCommand, Unit>(new WithdrawCommand(id, amount));
    public Task Close(Guid id) => Send<CloseAccountCommand, Unit>(new CloseAccountCommand(id));

    public Task<AccountSummary?> Summary(Guid id) => Ask<GetAccountQuery, AccountSummary?>(new GetAccountQuery(id));
}
