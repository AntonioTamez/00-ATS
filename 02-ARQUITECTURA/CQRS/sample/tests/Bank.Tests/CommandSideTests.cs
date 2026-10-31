using Bank.Commands;
using Bank.Domain;

namespace Bank.Tests;

/// <summary>The command side: it changes state and records events. It never reads the read model.</summary>
public class CommandSideTests
{
    [Fact]
    public async Task OpenAccount_ReturnsTheNewId_AndLogsAccountOpened()
    {
        var bank = new Harness();

        var id = await bank.Open("Ana", 100m);

        var logged = Assert.Single(bank.Log.ReadAfter(0));
        var opened = Assert.IsType<AccountOpened>(logged.Event);
        Assert.Equal((id, "Ana", 100m), (opened.AccountId, opened.Owner, opened.OverdraftLimit));
    }

    [Fact]
    public async Task EveryAcceptedCommand_AppendsExactlyItsEventsToTheLog()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");

        await bank.Deposit(id, 100m);
        await bank.Withdraw(id, 30m);

        Assert.Equal(
            [typeof(AccountOpened), typeof(MoneyDeposited), typeof(MoneyWithdrawn)],
            bank.Log.ReadAfter(0).Select(e => e.Event.GetType()));
    }

    [Fact]
    public async Task EventSequence_IsConsecutiveAndStartsAtOne()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await bank.Deposit(id, 1m);

        Assert.Equal([1L, 2L], bank.Log.ReadAfter(0).Select(e => e.Sequence));
    }

    [Fact]
    public async Task RejectedCommand_ChangesNothing_AndLogsNothing()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");

        await Assert.ThrowsAsync<DomainException>(() => bank.Withdraw(id, 10m));

        Assert.Single(bank.Log.ReadAfter(0));
    }

    [Fact]
    public async Task RejectedCommand_DoesNotCorruptLaterOnes()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await Assert.ThrowsAsync<DomainException>(() => bank.Withdraw(id, 10m));

        await bank.Deposit(id, 5m);
        await bank.Projector.RunPendingAsync();

        Assert.Equal(5m, (await bank.Summary(id))!.Balance);
    }

    [Fact]
    public async Task Commands_OnUnknownAccount_AreNotFound()
    {
        var bank = new Harness();
        var ghost = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => bank.Deposit(ghost, 1m));
        await Assert.ThrowsAsync<NotFoundException>(() => bank.Withdraw(ghost, 1m));
        await Assert.ThrowsAsync<NotFoundException>(() => bank.Close(ghost));
    }

    [Fact]
    public async Task Close_LogsAccountClosed()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");

        await bank.Close(id);

        Assert.IsType<AccountClosed>(bank.Log.ReadAfter(0).Last().Event);
    }
}
