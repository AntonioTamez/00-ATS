using Library.Adapters.Outbound;
using Library.Core.Domain;
using Library.Core.Ports.Outbound;

namespace Library.Tests;

/// <summary>
/// One contract, every driven adapter. If a new adapter passes these tests,
/// the core can use it without knowing which one it got.
/// </summary>
public class RepositoryContractTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"library-{Guid.NewGuid():N}.json");

    public void Dispose() { if (File.Exists(_file)) File.Delete(_file); }

    public static TheoryData<string> Adapters => new() { "memory", "json" };

    private IBookRepository Create(string kind) =>
        kind == "memory" ? new InMemoryBookRepository() : new JsonFileBookRepository(_file);

    [Theory, MemberData(nameof(Adapters))]
    public async Task Add_ThenGet_ReturnsSameBook(string kind)
    {
        var repo = Create(kind);
        var book = Book.Create("DDD");

        await repo.AddAsync(book, default);

        var found = await repo.GetByIdAsync(book.Id, default);
        Assert.Equal("DDD", found!.Title);
    }

    [Theory, MemberData(nameof(Adapters))]
    public async Task Update_PersistsLoan(string kind)
    {
        var repo = Create(kind);
        var book = Book.Create("DDD");
        await repo.AddAsync(book, default);

        book.Lend("Ana", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.UpdateAsync(book, default);

        var found = await repo.GetByIdAsync(book.Id, default);
        Assert.Equal("Ana", found!.CurrentLoan!.Borrower);
    }

    [Theory, MemberData(nameof(Adapters))]
    public async Task Get_Unknown_ReturnsNull(string kind) =>
        Assert.Null(await Create(kind).GetByIdAsync(Guid.NewGuid(), default));

    [Theory, MemberData(nameof(Adapters))]
    public async Task List_ReturnsAllBooks(string kind)
    {
        var repo = Create(kind);
        await repo.AddAsync(Book.Create("A"), default);
        await repo.AddAsync(Book.Create("B"), default);

        Assert.Equal(2, (await repo.ListAsync(default)).Count);
    }
}
