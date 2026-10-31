namespace Bank.Domain;

public sealed class DomainException(string message) : Exception(message);

public interface IDomainEvent
{
    Guid AccountId { get; }
    DateTimeOffset At { get; }
}

public sealed record AccountOpened(Guid AccountId, DateTimeOffset At, string Owner, decimal OverdraftLimit) : IDomainEvent;
public sealed record MoneyDeposited(Guid AccountId, DateTimeOffset At, decimal Amount, decimal BalanceAfter) : IDomainEvent;
public sealed record MoneyWithdrawn(Guid AccountId, DateTimeOffset At, decimal Amount, decimal BalanceAfter) : IDomainEvent;
public sealed record AccountClosed(Guid AccountId, DateTimeOffset At) : IDomainEvent;

public enum AccountStatus { Open, Closed }

/// <summary>
/// The WRITE model. It only exists to protect the rules of a change; nobody reads it to show data.
/// </summary>
public sealed class Account
{
    private readonly List<IDomainEvent> _events = [];

    private Account() { }

    public Guid Id { get; private set; }
    public string Owner { get; private set; } = "";
    public decimal OverdraftLimit { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }

    public static Account Open(string owner, decimal overdraftLimit, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new DomainException("The owner is required.");
        if (overdraftLimit < 0) throw new DomainException("The overdraft limit cannot be negative.");
        EnsureMoney(overdraftLimit);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Owner = owner.Trim(),
            OverdraftLimit = overdraftLimit,
            Status = AccountStatus.Open,
        };
        account._events.Add(new AccountOpened(account.Id, now, account.Owner, overdraftLimit));
        return account;
    }

    public void Deposit(decimal amount, DateTimeOffset now)
    {
        EnsureOpen();
        EnsurePositiveMoney(amount);

        Balance += amount;
        _events.Add(new MoneyDeposited(Id, now, amount, Balance));
    }

    public void Withdraw(decimal amount, DateTimeOffset now)
    {
        EnsureOpen();
        EnsurePositiveMoney(amount);
        if (Balance - amount < -OverdraftLimit)
            throw new DomainException($"Insufficient funds: balance {Balance:0.00}, overdraft limit {OverdraftLimit:0.00}.");

        Balance -= amount;
        _events.Add(new MoneyWithdrawn(Id, now, amount, Balance));
    }

    public void Close(DateTimeOffset now)
    {
        EnsureOpen();
        if (Balance != 0) throw new DomainException("Only an account with zero balance can be closed.");

        Status = AccountStatus.Closed;
        _events.Add(new AccountClosed(Id, now));
    }

    /// <summary>Hands the pending events to whoever persists the account, and forgets them.</summary>
    public IReadOnlyList<IDomainEvent> PullEvents()
    {
        var pending = _events.ToList();
        _events.Clear();
        return pending;
    }

    private void EnsureOpen()
    {
        if (Status == AccountStatus.Closed) throw new DomainException("The account is closed.");
    }

    private static void EnsurePositiveMoney(decimal amount)
    {
        if (amount <= 0) throw new DomainException("The amount must be positive.");
        EnsureMoney(amount);
    }

    private static void EnsureMoney(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount) throw new DomainException("Amounts have at most 2 decimals.");
    }
}

/// <summary>Persists the state AND its events together (a simplified outbox).</summary>
public interface IAccountRepository
{
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);
}
