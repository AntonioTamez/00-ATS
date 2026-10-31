using Library.Core.Domain;
using Library.Core.Ports.Inbound;
using Library.Core.Services;

namespace Library.Adapters.Cli;

/// <summary>
/// Second driving adapter: same core, different door. Commands: add &lt;title&gt; | list | lend &lt;id&gt; &lt;borrower&gt; | return &lt;id&gt;.
/// </summary>
public sealed class CliRunner(IAddBook add, ILendBook lend, IReturnBook ret, IListBooks list, TextWriter output)
{
    public async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        try
        {
            switch (args)
            {
                case ["add", .. var title] when title.Length > 0:
                    Print(await add.ExecuteAsync(new AddBookCommand(string.Join(' ', title)), ct));
                    return 0;
                case ["list"]:
                    foreach (var b in await list.ExecuteAsync(ct)) Print(b);
                    return 0;
                case ["lend", var id, .. var who] when Guid.TryParse(id, out var bookId):
                    Print(await lend.ExecuteAsync(new LendBookCommand(bookId, string.Join(' ', who)), ct));
                    return 0;
                case ["return", var id] when Guid.TryParse(id, out var bookId):
                    Print(await ret.ExecuteAsync(bookId, ct));
                    return 0;
                default:
                    await output.WriteLineAsync("Usage: add <title> | list | lend <id> <borrower> | return <id>");
                    return 2;
            }
        }
        catch (Exception e) when (e is DomainException or BookNotFoundException)
        {
            await output.WriteLineAsync($"Error: {e.Message}");
            return 1;
        }
    }

    private void Print(BookDto b) =>
        output.WriteLine($"{b.Id}  {b.Title}  {(b.IsAvailable ? "available" : $"on loan to {b.Borrower} until {b.DueAt:yyyy-MM-dd}")}");
}
