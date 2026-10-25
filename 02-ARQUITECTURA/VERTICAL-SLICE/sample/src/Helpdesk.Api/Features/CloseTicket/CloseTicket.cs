using Helpdesk.Api.Common;

namespace Helpdesk.Api.Features.CloseTicket;

public sealed record Request(string Resolution);

public sealed record Response(Guid Id, string Status, DateTimeOffset ClosedAt);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<Response> Handle(Guid id, Request request, CancellationToken ct)
    {
        var ticket = await db.Tickets.FindAsync([id], ct)
                     ?? throw new NotFoundException($"Ticket '{id}' was not found.");

        ticket.Close(request.Resolution, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);

        return new Response(ticket.Id, ticket.Status.ToString(), ticket.ClosedAt!.Value);
    }
}

public static class Endpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/tickets/{id:guid}/close", async (Guid id, Request request, Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(id, request, ct)));
}
