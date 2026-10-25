using System.Text.Json;
using Library.Core.Domain;
using Library.Core.Ports.Outbound;

namespace Library.Adapters.Outbound;

/// <summary>
/// Driven adapter #2 for the same port: persists to a JSON file.
/// The stored shape (BookRecord) is private to the adapter; the core never sees it.
/// </summary>
public sealed class JsonFileBookRepository(string path) : IBookRepository
{
    private sealed record BookRecord(Guid Id, string Title, string? Borrower, DateTimeOffset? LentAt, DateTimeOffset? DueAt);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task AddAsync(Book book, CancellationToken ct) => UpsertAsync(book, ct);
    public Task UpdateAsync(Book book, CancellationToken ct) => UpsertAsync(book, ct);

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(ct)).FirstOrDefault(b => b.Id == id);

    public async Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).Select(ToDomain).ToList(); }
        finally { _gate.Release(); }
    }

    private async Task UpsertAsync(Book book, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var records = (await ReadAsync(ct)).Where(r => r.Id != book.Id).ToList();
            records.Add(ToRecord(book));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(records), ct);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<BookRecord>> ReadAsync(CancellationToken ct) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<List<BookRecord>>(await File.ReadAllTextAsync(path, ct)) ?? []
            : [];

    private static BookRecord ToRecord(Book b) =>
        new(b.Id, b.Title, b.CurrentLoan?.Borrower, b.CurrentLoan?.LentAt, b.CurrentLoan?.DueAt);

    private static Book ToDomain(BookRecord r) =>
        Book.Rehydrate(r.Id, r.Title,
            r.Borrower is null ? null : new Loan(r.Borrower, r.LentAt!.Value, r.DueAt!.Value));
}
