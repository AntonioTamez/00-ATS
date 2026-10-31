namespace Library.Core.Ports.Inbound;

/// <summary>Plain data crossing the hexagon border. Entities never leave the core.</summary>
public sealed record BookDto(Guid Id, string Title, bool IsAvailable, string? Borrower, DateTimeOffset? DueAt);

public sealed record AddBookCommand(string Title);
public sealed record LendBookCommand(Guid BookId, string Borrower);

// Inbound (driving / primary) ports: what the outside world can ASK the application to do.
// Defined by the core, called by driving adapters (REST, CLI, tests...).
public interface IAddBook { Task<BookDto> ExecuteAsync(AddBookCommand command, CancellationToken ct = default); }
public interface ILendBook { Task<BookDto> ExecuteAsync(LendBookCommand command, CancellationToken ct = default); }
public interface IReturnBook { Task<BookDto> ExecuteAsync(Guid bookId, CancellationToken ct = default); }
public interface IListBooks { Task<IReadOnlyList<BookDto>> ExecuteAsync(CancellationToken ct = default); }
