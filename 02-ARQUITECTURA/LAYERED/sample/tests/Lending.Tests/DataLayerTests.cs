using Lending.Data;

namespace Lending.Tests;

/// <summary>The bottom layer alone: it knows tables and queries, nothing about rules.</summary>
public class DataLayerTests
{
    private static (LendingDatabase Db, BookRepository Books, MemberRepository Members, LoanRepository Loans) Create()
    {
        var db = new LendingDatabase();
        return (db, new BookRepository(db), new MemberRepository(db), new LoanRepository(db));
    }

    [Fact]
    public void Insert_AssignsConsecutiveIds()
    {
        var (_, books, _, _) = Create();

        var first = books.Insert(new BookRecord { Title = "A", Author = "x" });
        var second = books.Insert(new BookRecord { Title = "B", Author = "y" });

        Assert.Equal((1, 2), (first.Id, second.Id));
    }

    [Fact]
    public void RecordsReadFromTheDatabase_AreCopies_SoChangingThemChangesNothingUntilUpdated()
    {
        var (_, books, _, _) = Create();
        var saved = books.Insert(new BookRecord { Title = "A", Author = "x" });

        var read = books.GetById(saved.Id)!;
        read.Title = "Changed in memory";

        Assert.Equal("A", books.GetById(saved.Id)!.Title);
    }

    [Fact]
    public void GetById_OfAMissingRow_ReturnsNull()
    {
        var (_, books, members, loans) = Create();

        Assert.Null(books.GetById(99));
        Assert.Null(members.GetById(99));
        Assert.Null(loans.GetById(99));
    }

    [Fact]
    public void Members_CanBeFoundByEmail_IgnoringCase()
    {
        var (_, _, members, _) = Create();
        members.Insert(new MemberRecord { Name = "Ana", Email = "Ana@x.com" });

        Assert.Equal("Ana", members.GetByEmail("ana@X.COM")!.Name);
    }

    [Fact]
    public void ActiveLoanQueries_IgnoreReturnedLoans()
    {
        var (_, _, _, loans) = Create();
        var day = new DateOnly(2026, 1, 1);
        var open = loans.Insert(new LoanRecord { BookId = 1, MemberId = 1, BorrowedOn = day, DueOn = day.AddDays(14) });
        var closed = loans.Insert(new LoanRecord { BookId = 2, MemberId = 1, BorrowedOn = day, DueOn = day.AddDays(14), ReturnedOn = day.AddDays(3) });

        Assert.Equal([open.Id], loans.GetActiveByMember(1).Select(l => l.Id));
        Assert.Equal(open.Id, loans.GetActiveByBook(1)!.Id);
        Assert.Null(loans.GetActiveByBook(2));
        Assert.Equal([1], loans.GetBorrowedBookIds());
        Assert.Equal([open.Id, closed.Id], loans.GetByMember(1).Select(l => l.Id));
    }

    [Fact]
    public void Update_ReplacesTheStoredRow()
    {
        var (_, _, _, loans) = Create();
        var day = new DateOnly(2026, 1, 1);
        var saved = loans.Insert(new LoanRecord { BookId = 1, MemberId = 1, BorrowedOn = day, DueOn = day.AddDays(14) });

        saved.ReturnedOn = day.AddDays(2);
        loans.Update(saved);

        Assert.False(loans.GetById(saved.Id)!.IsActive);
    }

    [Fact]
    public void Update_OfAMissingRow_Fails() =>
        Assert.Throws<InvalidOperationException>(() => Create().Loans.Update(new LoanRecord { Id = 42 }));
}
