using Ledger.Api;
using Ledger.Domain;
using Ledger.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLedger();
builder.Services.AddExceptionHandler<LedgerExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// ---------------- commands: each one appends EVENTS; nothing is ever updated ----------------

app.MapPost("/items", async (RegisterRequest r, StockService stock) =>
{
    await stock.RegisterAsync(r.Sku ?? "", r.Name ?? "");
    return Results.Created($"/items/{r.Sku!.Trim().ToUpperInvariant()}", new { sku = r.Sku.Trim().ToUpperInvariant() });
});

app.MapPost("/items/{sku}/receive", async (string sku, ReceiveRequest r, StockService stock) =>
{
    await stock.ReceiveAsync(sku, r.Quantity, r.Supplier);
    return Results.NoContent();
});

app.MapPost("/items/{sku}/ship", async (string sku, ShipRequest r, StockService stock) =>
{
    await stock.ShipAsync(sku, r.Quantity, r.Reference ?? "");
    return Results.NoContent();
});

app.MapPost("/items/{sku}/adjust", async (string sku, AdjustRequest r, StockService stock) =>
{
    await stock.AdjustAsync(sku, r.Delta, r.Reason ?? "");
    return Results.NoContent();
});

app.MapPost("/items/{sku}/discontinue", async (string sku, StockService stock) =>
{
    await stock.DiscontinueAsync(sku);
    return Results.NoContent();
});

// ---------------- queries ----------------

// Read model (a projection). It catches up on every read, so it shows your own writes.
app.MapGet("/items", async (StockProjection projection) =>
{
    await projection.CatchUpAsync();
    return Results.Ok(projection.List());
});

app.MapGet("/items/{sku}", async (string sku, StockProjection projection) =>
{
    await projection.CatchUpAsync();
    return projection.Get(sku) is { } item ? Results.Ok(item) : Results.NotFound();
});

app.MapGet("/items/{sku}/movements", async (string sku, StockProjection projection) =>
{
    await projection.CatchUpAsync();
    return projection.Movements(sku) is { } movements ? Results.Ok(movements) : Results.NotFound();
});

// The source of truth: the item's own events, exactly as stored.
app.MapGet("/items/{sku}/events", async (string sku, StockRepository repository) =>
{
    var history = await repository.HistoryAsync(sku);
    return history.Count == 0
        ? Results.NotFound()
        : Results.Ok(history.Select(e => new { e.Version, e.Position, e.Type, e.SchemaVersion, e.OccurredAt, data = e.Data }));
});

// Time travel: the state as it was at a past moment, rebuilt by replaying only the events up to then.
app.MapGet("/items/{sku}/as-of", async (string sku, DateTimeOffset at, StockRepository repository) =>
    await repository.LoadAsOfAsync(sku, at) is { } item
        ? Results.Ok(new StockSummary(item.Sku, item.Name, item.OnHand, item.Discontinued))
        : Results.NotFound());

app.MapPost("/admin/rebuild-projection", async (StockProjection projection) =>
    Results.Ok(new { replayed = await projection.RebuildAsync() }));

app.Run();

public partial class Program;

namespace Ledger.Api
{
    public sealed record RegisterRequest(string? Sku, string? Name);
    public sealed record ReceiveRequest(int Quantity, string? Supplier);
    public sealed record ShipRequest(int Quantity, string? Reference);
    public sealed record AdjustRequest(int Delta, string? Reason);

    public sealed class LedgerExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title) = exception switch
            {
                DomainException => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated"),
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                ConcurrencyException => (StatusCodes.Status409Conflict, "Another change got there first; try again"),
                _ => (0, ""),
            };
            if (status == 0) return false;

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { title, detail = exception.Message, status }, cancellationToken);
            return true;
        }
    }
}
