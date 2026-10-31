using Helpdesk.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Api.Features.GetTicket;

// A query: reads straight from the database into the shape this screen needs.
// No entity, no domain logic: each slice picks the approach that fits it.

public sealed record Response(
    Guid Id, string Title, string Description, string Status,
    string? Assignee, string? Resolution, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt);

public sealed class Handler(AppDbContext db)
{
    public async Task<Response> Handle(Guid id, CancellationToken ct) =>
        await db.Tickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new Response(
                t.Id, t.Title, t.Description, t.Status.ToString(),
                t.Assignee, t.Resolution, t.OpenedAt, t.ClosedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"Ticket '{id}' was not found.");
}

public static class Endpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/tickets/{id:guid}", async (Guid id, Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(id, ct)));
}
