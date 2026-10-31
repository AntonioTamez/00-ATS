using Helpdesk.Api.Common;

namespace Helpdesk.Api.Features.OpenTicket;

// A vertical slice: everything this request needs, from HTTP to storage, in one place.

public sealed record Request(string Title, string? Description);

public sealed record Response(Guid Id, string Title, string Status);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        var ticket = Ticket.Open(request.Title, request.Description, clock.GetUtcNow());

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return new Response(ticket.Id, ticket.Title, ticket.Status.ToString());
    }
}

public static class Endpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/tickets", async (Request request, Handler handler, CancellationToken ct) =>
        {
            var response = await handler.Handle(request, ct);
            return Results.Created($"/api/tickets/{response.Id}", response);
        });
}
