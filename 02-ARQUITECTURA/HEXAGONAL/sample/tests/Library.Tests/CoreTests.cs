using Library.Core.Domain;
using Library.Core.Ports.Inbound;
using Library.Core.Ports.Outbound;
using Library.Core.Services;

namespace Library.Tests;

public class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_RejectsEmptyTitle() => Assert.Throws<DomainException>(() => Book.Create("  "));

    [Fact]
    public void Lend_SetsDueDateFourteenDaysAhead()
    {
        var book = Book.Create("DDD");
        book.Lend("Ana", Now);

        Assert.False(book.IsAvailable);
        Assert.Equal(Now.AddDays(Book.LoanDays), book.CurrentLoan!.DueAt);
    }

    [Fact]
    public void Lend_WhenAlreadyOnLoan_Throws()
    {
        var book = Book.Create("DDD");
        book.Lend("Ana", Now);

        Assert.Throws<DomainException>(() => book.Lend("Luis", Now));
    }

    [Fact]
    public void Lend_RequiresBorrower() =>
        Assert.Throws<DomainException>(() => Book.Create("DDD").Lend(" ", Now));

    [Fact]
    public void Return_WhenNotOnLoan_Throws() =>
        Assert.Throws<DomainException>(() => Book.Create("DDD").Return());
}

/// <summary>Services are tested through the inbound ports using hand-written driven fakes.</summary>
public class ServiceTests
{
    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private sealed class SpyNotifier : IOverdueNotifier
    {
        public List<string> Notified { get; } = [];
        public Task NotifyOverdueAsync(Book book, Loan loan, CancellationToken ct)
        {
            Notified.Add($"{book.Title}:{loan.Borrower}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepo : IBookRepository
    {
        private readonly Dictionary<Guid, Book> _items = new();
        public Task AddAsync(Book b, CancellationToken ct) { _items[b.Id] = b; return Task.CompletedTask; }
        public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Book>>(_items.Values.ToList());
        public Task UpdateAsync(Book b, CancellationToken ct) { _items[b.Id] = b; return Task.CompletedTask; }
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lend_ThenReturnOnTime_DoesNotNotify()
    {
        var repo = new FakeRepo(); var clock = new FakeClock(Now); var spy = new SpyNotifier();
        var book = await new AddBookService(repo).ExecuteAsync(new AddBookCommand("DDD"));
        await new LendBookService(repo, clock).ExecuteAsync(new LendBookCommand(book.Id, "Ana"));

        clock.UtcNow = Now.AddDays(Book.LoanDays);
        var returned = await new ReturnBookService(repo, clock, spy).ExecuteAsync(book.Id);

        Assert.True(returned.IsAvailable);
        Assert.Empty(spy.Notified);
    }

    [Fact]
    public async Task Return_AfterDueDate_NotifiesOverdue()
    {
        var repo = new FakeRepo(); var clock = new FakeClock(Now); var spy = new SpyNotifier();
        var book = await new AddBookService(repo).ExecuteAsync(new AddBookCommand("DDD"));
        await new LendBookService(repo, clock).ExecuteAsync(new LendBookCommand(book.Id, "Ana"));

        clock.UtcNow = Now.AddDays(Book.LoanDays + 1);
        await new ReturnBookService(repo, clock, spy).ExecuteAsync(book.Id);

        Assert.Equal(["DDD:Ana"], spy.Notified);
    }

    [Fact]
    public async Task Lend_UnknownBook_ThrowsNotFound() =>
        await Assert.ThrowsAsync<BookNotFoundException>(() =>
            new LendBookService(new FakeRepo(), new FakeClock(Now)).ExecuteAsync(new LendBookCommand(Guid.NewGuid(), "Ana")));

    [Fact]
    public async Task List_ReturnsBooksOrderedByTitle()
    {
        var repo = new FakeRepo();
        var add = new AddBookService(repo);
        await add.ExecuteAsync(new AddBookCommand("B"));
        await add.ExecuteAsync(new AddBookCommand("A"));

        var list = await new ListBooksService(repo).ExecuteAsync();

        Assert.Equal(["A", "B"], list.Select(b => b.Title));
    }
}
