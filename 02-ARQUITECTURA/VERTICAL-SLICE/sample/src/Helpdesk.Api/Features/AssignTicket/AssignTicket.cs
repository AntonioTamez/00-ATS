using Helpdesk.Api.Common;

namespace Helpdesk.Api.Features.AssignTicket;

public sealed record Request(string Assignee);

public sealed record Response(Guid Id, string Status, string Assignee);

public sealed class Handler(AppDbContext db)
{
    public async Task<Response> Handle(Guid id, Request request, CancellationToken ct)
    {
        var ticket = await db.Tickets.FindAsync([id], ct)
                     ?? throw new NotFoundException($"Ticket '{id}' was not found.");

        ticket.Assign(request.Assignee);
        await db.SaveChangesAsync(ct);

        return new Response(ticket.Id, ticket.Status.ToString(), ticket.Assignee!);
    }
}

public static class Endpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/tickets/{id:guid}/assign", async (Guid id, Request request, Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(id, request, ct)));
}
