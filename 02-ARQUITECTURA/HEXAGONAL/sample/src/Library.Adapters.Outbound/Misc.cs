using Library.Core.Domain;
using Library.Core.Ports.Outbound;

namespace Library.Adapters.Outbound;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Driven adapter for notifications. A real one could send e-mail without touching the core.</summary>
public sealed class ConsoleOverdueNotifier(TextWriter? output = null) : IOverdueNotifier
{
    private readonly TextWriter _out = output ?? Console.Error;

    public async Task NotifyOverdueAsync(Book book, Loan loan, CancellationToken ct) =>
        await _out.WriteLineAsync($"[overdue] '{book.Title}' returned late by {loan.Borrower} (due {loan.DueAt:yyyy-MM-dd}).");
}
