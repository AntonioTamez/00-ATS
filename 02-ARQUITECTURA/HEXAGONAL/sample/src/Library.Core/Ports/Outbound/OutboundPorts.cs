using Library.Core.Domain;

namespace Library.Core.Ports.Outbound;

// Outbound (driven / secondary) ports: what the application NEEDS from the outside world.
// Defined by the core, implemented by driven adapters (database, clock, e-mail...).
public interface IBookRepository
{
    Task AddAsync(Book book, CancellationToken ct);
    Task<Book?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Book>> ListAsync(CancellationToken ct);
    Task UpdateAsync(Book book, CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IOverdueNotifier
{
    Task NotifyOverdueAsync(Book book, Loan loan, CancellationToken ct);
}
