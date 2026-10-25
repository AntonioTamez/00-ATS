namespace Lending.Data;

// The shape of the TABLES. Plain bags of columns with public setters: this is what a data-access layer returns.

public sealed class BookRecord
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";

    internal BookRecord Copy() => (BookRecord)MemberwiseClone();
}

public sealed class MemberRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    internal MemberRecord Copy() => (MemberRecord)MemberwiseClone();
}

public sealed class LoanRecord
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int MemberId { get; set; }
    public DateOnly BorrowedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public decimal LateFee { get; set; }

    public bool IsActive => ReturnedOn is null;

    internal LoanRecord Copy() => (LoanRecord)MemberwiseClone();
}
