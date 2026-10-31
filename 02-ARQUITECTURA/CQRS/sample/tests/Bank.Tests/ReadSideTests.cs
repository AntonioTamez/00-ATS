using Bank.Queries;

namespace Bank.Tests;

/// <summary>The read side and the projector that feeds it: eventual consistency, idempotence and rebuild.</summary>
public class ReadSideTests
{
    [Fact]
    public async Task ReadModel_IsStale_UntilTheProjectorRuns()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");

        Assert.Null(await bank.Summary(id));                 // the command succeeded, the screen does not know yet

        Assert.Equal(1, await bank.Projector.RunPendingAsync());

        Assert.NotNull(await bank.Summary(id));
    }

    [Fact]
    public async Task Projector_AppliesBalancesAndCounters()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana", 100m);
        await bank.Deposit(id, 80m);
        await bank.Withdraw(id, 30m);
        await bank.Projector.RunPendingAsync();

        var summary = (await bank.Summary(id))!;

        Assert.Equal((id, "Ana", 50m, 100m, "Open", 2),
            (summary.Id, summary.Owner, summary.Balance, summary.OverdraftLimit, summary.Status, summary.TransactionCount));
    }

    [Fact]
    public async Task Projector_RunningTwice_DoesNotApplyEventsTwice()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await bank.Deposit(id, 10m);

        await bank.Projector.RunPendingAsync();
        var second = await bank.Projector.RunPendingAsync();

        Assert.Equal(0, second);
        Assert.Equal(1, (await bank.Summary(id))!.TransactionCount);
    }

    [Fact]
    public async Task Projector_ResumesFromWhereItStopped()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await bank.Projector.RunPendingAsync();
        await bank.Deposit(id, 10m);

        Assert.Equal(1, await bank.Projector.RunPendingAsync());
        Assert.Equal(10m, (await bank.Summary(id))!.Balance);
    }

    [Fact]
    public async Task Rebuild_ReproducesTheSameReadModelFromTheLog()
    {
        var bank = new Harness();
        var a = await bank.Open("Ana");
        var b = await bank.Open("Bob", 20m);
        await bank.Deposit(a, 100m);
        await bank.Withdraw(b, 15m);
        await bank.Close(await bank.Open("Cleo"));
        await bank.Projector.RunPendingAsync();
        var before = await bank.Ask<ListAccountsQuery, IReadOnlyList<AccountSummary>>(new ListAccountsQuery());

        var replayed = await bank.Projector.RebuildAsync();

        Assert.Equal(before.Count + 0, (await bank.Ask<ListAccountsQuery, IReadOnlyList<AccountSummary>>(new ListAccountsQuery())).Count);
        Assert.Equal(before, await bank.Ask<ListAccountsQuery, IReadOnlyList<AccountSummary>>(new ListAccountsQuery()));
        Assert.Equal(bank.Log.ReadAfter(0).Count, replayed);
    }

    [Fact]
    public async Task ClosedAccount_ShowsAsClosed()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await bank.Close(id);
        await bank.Projector.RunPendingAsync();

        Assert.Equal("Closed", (await bank.Summary(id))!.Status);
    }

    [Fact]
    public async Task Transactions_AreListedNewestFirst_WithBalanceAfter()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        await bank.Deposit(id, 100m);
        await bank.Withdraw(id, 30m);
        await bank.Projector.RunPendingAsync();

        var items = (await bank.Ask<GetTransactionsQuery, IReadOnlyList<TransactionItem>?>(new GetTransactionsQuery(id)))!;

        Assert.Equal([("Withdrawal", 30m, 70m), ("Deposit", 100m, 100m)], items.Select(t => (t.Type, t.Amount, t.BalanceAfter)));
    }

    [Fact]
    public async Task Transactions_SupportPaging_AndClampTheLimits()
    {
        var bank = new Harness();
        var id = await bank.Open("Ana");
        for (var i = 1; i <= 5; i++) await bank.Deposit(id, i);
        await bank.Projector.RunPendingAsync();

        var page = (await bank.Ask<GetTransactionsQuery, IReadOnlyList<TransactionItem>?>(new GetTransactionsQuery(id, Skip: 1, Take: 2)))!;
        var negative = (await bank.Ask<GetTransactionsQuery, IReadOnlyList<TransactionItem>?>(new GetTransactionsQuery(id, Skip: -5, Take: 0)))!;

        Assert.Equal([4m, 3m], page.Select(t => t.Amount));
        Assert.Single(negative);                              // Take is clamped to at least 1; Skip to at least 0
    }

    [Fact]
    public async Task Transactions_OfUnknownAccount_AreNull() =>
        Assert.Null(await new Harness().Ask<GetTransactionsQuery, IReadOnlyList<TransactionItem>?>(new GetTransactionsQuery(Guid.NewGuid())));

    [Fact]
    public async Task List_FiltersByOwner_IgnoringCase_AndOrdersByOwner()
    {
        var bank = new Harness();
        await bank.Open("bob");
        await bank.Open("Ana");
        await bank.Open("ANA");
        await bank.Projector.RunPendingAsync();

        var all = await bank.Ask<ListAccountsQuery, IReadOnlyList<AccountSummary>>(new ListAccountsQuery());
        var anas = await bank.Ask<ListAccountsQuery, IReadOnlyList<AccountSummary>>(new ListAccountsQuery("  ana "));

        Assert.Equal(["Ana", "ANA", "bob"], all.Select(a => a.Owner));
        Assert.Equal(2, anas.Count);
    }

    [Fact]
    public async Task Richest_ReturnsOpenAccountsOrderedByBalance_AndClampsCount()
    {
        var bank = new Harness();
        var a = await bank.Open("Ana");
        var b = await bank.Open("Bob");
        var c = await bank.Open("Cleo");
        await bank.Deposit(a, 10m);
        await bank.Deposit(b, 300m);
        await bank.Deposit(c, 200m);
        await bank.Withdraw(c, 200m);
        await bank.Close(c);
        await bank.Projector.RunPendingAsync();

        var top = await bank.Ask<GetRichestAccountsQuery, IReadOnlyList<AccountSummary>>(new GetRichestAccountsQuery(0));
        var all = await bank.Ask<GetRichestAccountsQuery, IReadOnlyList<AccountSummary>>(new GetRichestAccountsQuery(99));

        Assert.Equal(["Bob"], top.Select(t => t.Owner));      // Count 0 is clamped to 1
        Assert.Equal(["Bob", "Ana"], all.Select(t => t.Owner)); // closed accounts excluded
    }
}
