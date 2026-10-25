using Helpdesk.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Api.Features.ListTickets;

public sealed record Query(string? Status, int Page = 1, int PageSize = 20);

public sealed record Item(Guid Id, string Title, string Status, string? Assignee);

public sealed record Response(IReadOnlyList<Item> Items, int Page, int PageSize, int Total);

public sealed class Handler(AppDbContext db)
{
    public const int MaxPageSize = 50;

    public async Task<Response> Handle(Query query, CancellationToken ct)
    {
        // Request validation belongs to the slice that owns the request.
        if (query.Page < 1)
            throw new RequestValidationException("Page must be 1 or greater.");
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new RequestValidationException($"PageSize must be between 1 and {MaxPageSize}.");

        var tickets = db.Tickets.AsNoTracking();

        if (query.Status is not null)
        {
            if (!Enum.TryParse<TicketStatus>(query.Status, ignoreCase: true, out var status))
                throw new RequestValidationException($"Unknown status '{query.Status}'.");
            tickets = tickets.Where(t => t.Status == status);
        }

        var total = await tickets.CountAsync(ct);
        var items = (await tickets
                .OrderBy(t => t.OpenedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(ct))
            .Select(t => new Item(t.Id, t.Title, t.Status.ToString(), t.Assignee))
            .ToList();

        return new Response(items, query.Page, query.PageSize, total);
    }
}

public static class Endpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/tickets", async (string? status, int? page, int? pageSize, Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new Query(status, page ?? 1, pageSize ?? 20), ct)));
}
