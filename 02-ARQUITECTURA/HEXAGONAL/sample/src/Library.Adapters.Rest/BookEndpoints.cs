using Library.Core.Ports.Inbound;

namespace Library.Adapters.Rest;

public sealed record AddBookRequest(string Title);
public sealed record LendBookRequest(string Borrower);

/// <summary>Driving adapter: translates HTTP into calls on the inbound ports, nothing else.</summary>
public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books");

        group.MapGet("/", async (IListBooks port, CancellationToken ct) => Results.Ok(await port.ExecuteAsync(ct)));

        group.MapPost("/", async (AddBookRequest body, IAddBook port, CancellationToken ct) =>
        {
            var book = await port.ExecuteAsync(new AddBookCommand(body.Title), ct);
            return Results.Created($"/api/books/{book.Id}", book);
        });

        group.MapPost("/{id:guid}/lend", async (Guid id, LendBookRequest body, ILendBook port, CancellationToken ct) =>
            Results.Ok(await port.ExecuteAsync(new LendBookCommand(id, body.Borrower), ct)));

        group.MapPost("/{id:guid}/return", async (Guid id, IReturnBook port, CancellationToken ct) =>
            Results.Ok(await port.ExecuteAsync(id, ct)));

        return app;
    }
}
