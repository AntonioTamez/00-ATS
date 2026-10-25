using Lending.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Lending.Tests;

internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}

/// <summary>
/// The business layer on top of the REAL data layer (in memory). In a layered design the business classes depend
/// directly on the data classes, so this is how they are tested: there is no seam to put a fake in.
/// </summary>
public class BusinessLayerTests
{
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly CatalogService _catalog;
    private readonly MembershipService _membership;
    private readonly LoanService _loans;

    public BusinessLayerTests()
    {
        var provider = new ServiceCollection().AddLendingBusiness().AddSingleton<TimeProvider>(_time).BuildServiceProvider();
        var scope = provider.CreateScope().ServiceProvider;
        _catalog = scope.GetRequiredService<CatalogService>();
        _membership = scope.GetRequiredService<MembershipService>();
        _loans = scope.GetRequiredService<LoanService>();
    }

    private int NewBook(string title = "Book") => _catalog.AddBook(title, "Someone").Id;

    private int NewMember(string email = "ana@x.com") => _membership.Register("Ana", email).Id;

    // ---------- catalog ----------

    [Fact]
    public void ANewBook_IsAvailable()
    {
        var book = _catalog.AddBook("  Domain-Driven Design  ", " Evans ");

        Assert.Equal(("Domain-Driven Design", "Evans", true), (book.Title, book.Author, book.Available));
    }

    [Theory]
    [InlineData("", "Evans")]
    [InlineData("DDD", " ")]
    public void ABookNeedsATitleAndAnAuthor(string title, string author) =>
        Assert.Throws<BusinessRuleException>(() => _catalog.AddBook(title, author));

    [Fact]
    public void AnUnknownBook_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => _catalog.GetBook(99));

    // ---------- membership ----------

    [Fact]
    public void ANewMember_IsRegistered()
    {
        var member = _membership.Register(" Ana ", " ana@x.com ");

        Assert.Equal(("Ana", "ana@x.com"), (member.Name, member.Email));
    }

    [Theory]
    [InlineData("", "ana@x.com")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "not-an-email")]
    public void AMemberNeedsANameAndAValidEmail(string name, string email) =>
        Assert.Throws<BusinessRuleException>(() => _membership.Register(name, email));

    [Fact]
    public void TheSameEmail_CannotRegisterTwice()
    {
        _membership.Register("Ana", "ana@x.com");

        Assert.Throws<BusinessRuleException>(() => _membership.Register("Other", "ANA@x.com"));
    }

    // ---------- borrowing ----------

    [Fact]
    public void Borrowing_SetsTheDueDate14DaysAhead_AndMakesTheBookUnavailable()
    {
        var member = NewMember();
        var book = NewBook();

        var loan = _loans.Borrow(member, book);

        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15), null), (loan.BorrowedOn, loan.DueOn, loan.ReturnedOn));
        Assert.False(_catalog.GetBook(book).Available);
    }

    [Fact]
    public void ABookOnLoan_CannotBeBorrowedByAnyoneElse()
    {
        var book = NewBook();
        _loans.Borrow(NewMember("a@x.com"), book);

        var ex = Assert.Throws<BusinessRuleException>(() => _loans.Borrow(NewMember("b@x.com"), book));

        Assert.Contains("already on loan", ex.Message);
    }

    [Fact]
    public void AMember_CannotHaveMoreThanThreeBooksAtOnce()
    {
        var member = NewMember();
        for (var i = 0; i < 3; i++) _loans.Borrow(member, NewBook($"Book {i}"));

        var ex = Assert.Throws<BusinessRuleException>(() => _loans.Borrow(member, NewBook("One too many")));

        Assert.Contains("3 books", ex.Message);
    }

    [Fact]
    public void ReturningABook_FreesTheMemberSlot_AndTheBook()
    {
        var member = NewMember();
        var books = Enumerable.Range(0, 3).Select(i => NewBook($"Book {i}")).ToList();
        var loans = books.Select(b => _loans.Borrow(member, b)).ToList();
        _loans.Return(loans[0].Id);

        var again = _loans.Borrow(member, NewBook("Fourth"));          // a slot is free again
        var sameBookByOther = _loans.Borrow(NewMember("b@x.com"), books[0]);

        Assert.NotEqual(0, again.Id);
        Assert.Equal(books[0], sameBookByOther.BookId);
    }

    [Fact]
    public void BorrowingWithAnUnknownMemberOrBook_IsNotFound()
    {
        Assert.Throws<NotFoundException>(() => _loans.Borrow(99, NewBook()));
        Assert.Throws<NotFoundException>(() => _loans.Borrow(NewMember(), 99));
    }

    // ---------- returning ----------

    [Fact]
    public void ReturningOnTime_HasNoFee()
    {
        var loan = _loans.Borrow(NewMember(), NewBook());
        _time.Advance(TimeSpan.FromDays(14));                           // exactly the due date

        var returned = _loans.Return(loan.Id);

        Assert.Equal((new DateOnly(2026, 3, 15), 0m), (returned.ReturnedOn, returned.LateFee));
    }

    [Fact]
    public void ReturningLate_ChargesHalfAnEuroPerDayLate()
    {
        var loan = _loans.Borrow(NewMember(), NewBook());
        _time.Advance(TimeSpan.FromDays(17));                           // 3 days after the due date

        var returned = _loans.Return(loan.Id);

        Assert.Equal(1.50m, returned.LateFee);
    }

    [Fact]
    public void ALoan_CannotBeReturnedTwice()
    {
        var loan = _loans.Borrow(NewMember(), NewBook());
        _loans.Return(loan.Id);

        Assert.Throws<BusinessRuleException>(() => _loans.Return(loan.Id));
    }

    [Fact]
    public void ReturningAnUnknownLoan_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => _loans.Return(99));

    [Fact]
    public void AMembersLoans_AreListedWithTitlesAndNames()
    {
        var member = NewMember();
        _loans.Borrow(member, NewBook("DDD"));

        var loan = Assert.Single(_loans.ListForMember(member));

        Assert.Equal(("DDD", "Ana"), (loan.BookTitle, loan.MemberName));
        Assert.Throws<NotFoundException>(() => _loans.ListForMember(99));
    }

    [Fact]
    public void TheCatalog_ShowsAvailabilityPerBook()
    {
        var onLoan = NewBook("On loan");
        NewBook("On the shelf");
        _loans.Borrow(NewMember(), onLoan);

        var books = _catalog.ListBooks();

        Assert.Equal([("On loan", false), ("On the shelf", true)], books.Select(b => (b.Title, b.Available)));
    }
}
