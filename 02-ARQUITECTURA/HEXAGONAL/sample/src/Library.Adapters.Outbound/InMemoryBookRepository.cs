using System.Collections.Concurrent;
using Library.Core.Domain;
using Library.Core.Ports.Outbound;

namespace Library.Adapters.Outbound;

/// <summary>Driven adapter #1 for IBookRepository. Also what tests and demos plug in.</summary>
public sealed class InMemoryBookRepository : IBookRepository
{
    private readonly ConcurrentDictionary<Guid, Book> _store = new();

    public Task AddAsync(Book book, CancellationToken ct)
    {
        _store[book.Id] = book;
        return Task.CompletedTask;
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Book>>(_store.Values.ToList());

    public Task UpdateAsync(Book book, CancellationToken ct)
    {
        _store[book.Id] = book;
        return Task.CompletedTask;
    }
}
