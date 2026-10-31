using Lending.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lending.Business;

// What the layer ABOVE sees. The Data layer's records never leave this layer.
public sealed record BookDto(int Id, string Title, string Author, bool Available);

public sealed record MemberDto(int Id, string Name, string Email);

public sealed record LoanDto(
    int Id, int BookId, string BookTitle, int MemberId, string MemberName,
    DateOnly BorrowedOn, DateOnly DueOn, DateOnly? ReturnedOn, decimal LateFee);

public sealed class BusinessRuleException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);

public sealed class CatalogService(BookRepository books, LoanRepository loans)
{
    public IReadOnlyList<BookDto> ListBooks()
    {
        var borrowed = loans.GetBorrowedBookIds();
        return books.GetAll().Select(b => new BookDto(b.Id, b.Title, b.Author, !borrowed.Contains(b.Id))).ToList();
    }

    public BookDto GetBook(int id)
    {
        var book = books.GetById(id) ?? throw new NotFoundException($"Book {id} was not found.");
        return new BookDto(book.Id, book.Title, book.Author, loans.GetActiveByBook(id) is null);
    }

    public BookDto AddBook(string title, string author)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new BusinessRuleException("The title is required.");
        if (string.IsNullOrWhiteSpace(author)) throw new BusinessRuleException("The author is required.");

        var saved = books.Insert(new BookRecord { Title = title.Trim(), Author = author.Trim() });
        return new BookDto(saved.Id, saved.Title, saved.Author, Available: true);
    }
}

public sealed class MembershipService(MemberRepository members)
{
    public MemberDto Register(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("The name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new BusinessRuleException("A valid email is required.");
        if (members.GetByEmail(email.Trim()) is not null) throw new BusinessRuleException("That email is already registered.");

        var saved = members.Insert(new MemberRecord { Name = name.Trim(), Email = email.Trim() });
        return new MemberDto(saved.Id, saved.Name, saved.Email);
    }

    public MemberDto Get(int id)
    {
        var member = members.GetById(id) ?? throw new NotFoundException($"Member {id} was not found.");
        return new MemberDto(member.Id, member.Name, member.Email);
    }
}

public sealed class LoanService(
    BookRepository books, MemberRepository members, LoanRepository loans, TimeProvider clock)
{
    public const int LoanDays = 14;
    public const int MaxActiveLoans = 3;
    public const decimal LateFeePerDay = 0.50m;

    public LoanDto Borrow(int memberId, int bookId)
    {
        var member = members.GetById(memberId) ?? throw new NotFoundException($"Member {memberId} was not found.");
        var book = books.GetById(bookId) ?? throw new NotFoundException($"Book {bookId} was not found.");

        if (loans.GetActiveByBook(bookId) is not null)
            throw new BusinessRuleException($"'{book.Title}' is already on loan.");
        if (loans.GetActiveByMember(memberId).Count >= MaxActiveLoans)
            throw new BusinessRuleException($"{member.Name} already has {MaxActiveLoans} books on loan.");

        var today = Today();
        var saved = loans.Insert(new LoanRecord { BookId = bookId, MemberId = memberId, BorrowedOn = today, DueOn = today.AddDays(LoanDays) });
        return ToDto(saved, book, member);
    }

    public LoanDto Return(int loanId)
    {
        var loan = loans.GetById(loanId) ?? throw new NotFoundException($"Loan {loanId} was not found.");
        if (!loan.IsActive) throw new BusinessRuleException("That loan was already returned.");

        var today = Today();
        loan.ReturnedOn = today;
        loan.LateFee = today > loan.DueOn ? (today.DayNumber - loan.DueOn.DayNumber) * LateFeePerDay : 0m;
        loans.Update(loan);

        return ToDto(loan, books.GetById(loan.BookId)!, members.GetById(loan.MemberId)!);
    }

    public IReadOnlyList<LoanDto> ListForMember(int memberId)
    {
        var member = members.GetById(memberId) ?? throw new NotFoundException($"Member {memberId} was not found.");
        return loans.GetByMember(memberId).Select(l => ToDto(l, books.GetById(l.BookId)!, member)).ToList();
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    // The "mapping tax" of layering: every record is copied into a DTO by hand.
    private static LoanDto ToDto(LoanRecord l, BookRecord book, MemberRecord member) =>
        new(l.Id, l.BookId, book.Title, l.MemberId, member.Name, l.BorrowedOn, l.DueOn, l.ReturnedOn, l.LateFee);
}

public static class BusinessRegistration
{
    /// <summary>The layer above needs only this call: it never has to know the Data layer exists.</summary>
    public static IServiceCollection AddLendingBusiness(this IServiceCollection services)
    {
        services.AddLendingData();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<LoanService>();
        return services;
    }
}
