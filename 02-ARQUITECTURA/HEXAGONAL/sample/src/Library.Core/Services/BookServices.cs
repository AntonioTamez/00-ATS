using Library.Core.Domain;
using Library.Core.Ports.Inbound;
using Library.Core.Ports.Outbound;

namespace Library.Core.Services;

public sealed class BookNotFoundException(Guid id) : Exception($"Book '{id}' was not found.");

internal static class BookMapper
{
    public static BookDto ToDto(Book b) =>
        new(b.Id, b.Title, b.IsAvailable, b.CurrentLoan?.Borrower, b.CurrentLoan?.DueAt);
}

// Services implement the inbound ports by orchestrating the domain and the outbound ports.

public sealed class AddBookService(IBookRepository books) : IAddBook
{
    public async Task<BookDto> ExecuteAsync(AddBookCommand command, CancellationToken ct = default)
    {
        var book = Book.Create(command.Title);
        await books.AddAsync(book, ct);
        return BookMapper.ToDto(book);
    }
}

public sealed class LendBookService(IBookRepository books, IClock clock) : ILendBook
{
    public async Task<BookDto> ExecuteAsync(LendBookCommand command, CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(command.BookId, ct) ?? throw new BookNotFoundException(command.BookId);
        book.Lend(command.Borrower, clock.UtcNow);
        await books.UpdateAsync(book, ct);
        return BookMapper.ToDto(book);
    }
}

public sealed class ReturnBookService(IBookRepository books, IClock clock, IOverdueNotifier notifier) : IReturnBook
{
    public async Task<BookDto> ExecuteAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(bookId, ct) ?? throw new BookNotFoundException(bookId);
        var loan = book.Return();
        await books.UpdateAsync(book, ct);

        if (loan.IsOverdueAt(clock.UtcNow))
            await notifier.NotifyOverdueAsync(book, loan, ct);

        return BookMapper.ToDto(book);
    }
}

public sealed class ListBooksService(IBookRepository books) : IListBooks
{
    public async Task<IReadOnlyList<BookDto>> ExecuteAsync(CancellationToken ct = default) =>
        (await books.ListAsync(ct)).OrderBy(b => b.Title).Select(BookMapper.ToDto).ToList();
}
