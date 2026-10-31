namespace Library.Core.Domain;

public sealed class DomainException(string message) : Exception(message);

public sealed record Loan(string Borrower, DateTimeOffset LentAt, DateTimeOffset DueAt)
{
    public bool IsOverdueAt(DateTimeOffset now) => now > DueAt;
}

/// <summary>Entity: owns the lending rules. Knows nothing about ports or adapters.</summary>
public sealed class Book
{
    public const int LoanDays = 14;

    public Guid Id { get; }
    public string Title { get; }
    public Loan? CurrentLoan { get; private set; }
    public bool IsAvailable => CurrentLoan is null;

    private Book(Guid id, string title, Loan? loan)
    {
        Id = id;
        Title = title;
        CurrentLoan = loan;
    }

    public static Book Create(string title)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new DomainException("A book title is required.");
        return new Book(Guid.NewGuid(), normalized, null);
    }

    /// <summary>Rebuilds a stored book. Used by driven adapters (persistence), never to create new books.</summary>
    public static Book Rehydrate(Guid id, string title, Loan? loan) => new(id, title, loan);

    public void Lend(string borrower, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(borrower))
            throw new DomainException("A borrower is required.");
        if (!IsAvailable)
            throw new DomainException("The book is already on loan.");

        CurrentLoan = new Loan(borrower.Trim(), now, now.AddDays(LoanDays));
    }

    /// <summary>Returns the finished loan so the caller can tell whether it was overdue.</summary>
    public Loan Return()
    {
        var loan = CurrentLoan ?? throw new DomainException("The book is not on loan.");
        CurrentLoan = null;
        return loan;
    }
}
