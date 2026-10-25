using Bank.Domain;

namespace Bank.Tests;

/// <summary>The write model: rules only.</summary>
public class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static Account Opened(decimal overdraft = 0m) => Account.Open("Ana", overdraft, Now);

    [Fact]
    public void Open_StartsOpenWithZeroBalance_AndRaisesAccountOpened()
    {
        var account = Account.Open("  Ana  ", 100m, Now);

        Assert.Equal("Ana", account.Owner);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(AccountStatus.Open, account.Status);
        var opened = Assert.IsType<AccountOpened>(Assert.Single(account.PullEvents()));
        Assert.Equal((account.Id, "Ana", 100m), (opened.AccountId, opened.Owner, opened.OverdraftLimit));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_WithoutOwner_Fails(string owner) =>
        Assert.Throws<DomainException>(() => Account.Open(owner, 0m, Now));

    [Fact]
    public void Open_WithNegativeOverdraft_Fails() =>
        Assert.Throws<DomainException>(() => Account.Open("Ana", -1m, Now));

    [Fact]
    public void Deposit_IncreasesBalance_AndRaisesEventWithBalanceAfter()
    {
        var account = Opened();
        account.PullEvents();

        account.Deposit(50m, Now);

        Assert.Equal(50m, account.Balance);
        var deposited = Assert.IsType<MoneyDeposited>(Assert.Single(account.PullEvents()));
        Assert.Equal((50m, 50m), (deposited.Amount, deposited.BalanceAfter));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.001)]
    public void Deposit_InvalidAmount_FailsAndChangesNothing(double amount)
    {
        var account = Opened();
        account.PullEvents();

        Assert.Throws<DomainException>(() => account.Deposit((decimal)amount, Now));

        Assert.Equal(0m, account.Balance);
        Assert.Empty(account.PullEvents());
    }

    [Fact]
    public void Withdraw_WithinBalance_Works()
    {
        var account = Opened();
        account.Deposit(100m, Now);

        account.Withdraw(40m, Now);

        Assert.Equal(60m, account.Balance);
    }

    [Fact]
    public void Withdraw_CanUseOverdraft_UpToTheLimit()
    {
        var account = Opened(overdraft: 50m);

        account.Withdraw(50m, Now);

        Assert.Equal(-50m, account.Balance);
    }

    [Fact]
    public void Withdraw_BeyondOverdraft_FailsAndChangesNothing()
    {
        var account = Opened(overdraft: 50m);
        account.PullEvents();

        var ex = Assert.Throws<DomainException>(() => account.Withdraw(50.01m, Now));

        Assert.Contains("Insufficient funds", ex.Message);
        Assert.Equal(0m, account.Balance);
        Assert.Empty(account.PullEvents());
    }

    [Fact]
    public void Close_WithZeroBalance_Works_AndRaisesAccountClosed()
    {
        var account = Opened();
        account.PullEvents();

        account.Close(Now);

        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.IsType<AccountClosed>(Assert.Single(account.PullEvents()));
    }

    [Fact]
    public void Close_WithMoney_Fails()
    {
        var account = Opened();
        account.Deposit(10m, Now);

        Assert.Throws<DomainException>(() => account.Close(Now));
    }

    [Fact]
    public void Close_WithDebt_Fails()
    {
        var account = Opened(overdraft: 10m);
        account.Withdraw(5m, Now);

        Assert.Throws<DomainException>(() => account.Close(Now));
    }

    [Fact]
    public void ClosedAccount_RejectsEveryOperation()
    {
        var account = Opened();
        account.Close(Now);

        Assert.Throws<DomainException>(() => account.Deposit(1m, Now));
        Assert.Throws<DomainException>(() => account.Withdraw(1m, Now));
        Assert.Throws<DomainException>(() => account.Close(Now));
    }

    [Fact]
    public void PullEvents_ReturnsEachEventOnce()
    {
        var account = Opened();

        Assert.Single(account.PullEvents());
        Assert.Empty(account.PullEvents());
    }
}
