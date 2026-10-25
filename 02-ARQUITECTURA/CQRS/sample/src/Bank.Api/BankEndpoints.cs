using Bank.Commands;
using Bank.Domain;
using Bank.Infrastructure;
using Bank.Queries;
using Microsoft.AspNetCore.Diagnostics;

namespace Bank.Api;

public sealed record OpenAccountRequest(string Owner, decimal OverdraftLimit);
public sealed record AmountRequest(decimal Amount);

public static class BankEndpoints
{
    public static IEndpointRouteBuilder MapBankEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts");

        // ---------- COMMANDS: change state, answer almost nothing ----------

        accounts.MapPost("/", async (OpenAccountRequest body, ICommandHandler<OpenAccountCommand, Guid> handler, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(new OpenAccountCommand(body.Owner, body.OverdraftLimit), ct);
            // Location points to the READ side, which may not know the account yet (eventual consistency).
            return Results.Created($"/accounts/{id}", new { id });
        });

        accounts.MapPost("/{id:guid}/deposit", async (Guid id, AmountRequest body, ICommandHandler<DepositCommand, Unit> handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DepositCommand(id, body.Amount), ct);
            return Results.NoContent();
        });

        accounts.MapPost("/{id:guid}/withdraw", async (Guid id, AmountRequest body, ICommandHandler<WithdrawCommand, Unit> handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new WithdrawCommand(id, body.Amount), ct);
            return Results.NoContent();
        });

        accounts.MapPost("/{id:guid}/close", async (Guid id, ICommandHandler<CloseAccountCommand, Unit> handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new CloseAccountCommand(id), ct);
            return Results.NoContent();
        });

        // ---------- QUERIES: read the read model, never change anything ----------

        accounts.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetAccountQuery, AccountSummary?> handler, CancellationToken ct) =>
            await handler.HandleAsync(new GetAccountQuery(id), ct) is { } summary ? Results.Ok(summary) : Results.NotFound());

        accounts.MapGet("/{id:guid}/transactions", async (Guid id, int? skip, int? take,
            IQueryHandler<GetTransactionsQuery, IReadOnlyList<TransactionItem>?> handler, CancellationToken ct) =>
            await handler.HandleAsync(new GetTransactionsQuery(id, skip ?? 0, take ?? 50), ct) is { } page
                ? Results.Ok(page) : Results.NotFound());

        accounts.MapGet("/", async (string? owner, IQueryHandler<ListAccountsQuery, IReadOnlyList<AccountSummary>> handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListAccountsQuery(owner), ct)));

        accounts.MapGet("/top", async (int? count, IQueryHandler<GetRichestAccountsQuery, IReadOnlyList<AccountSummary>> handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetRichestAccountsQuery(count ?? 3), ct)));

        return app;
    }
}

public sealed class BankExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (0, ""),
        };
        if (status == 0) return false;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { title, detail = exception.Message, status }, cancellationToken);
        return true;
    }
}

/// <summary>Runs the projector in the background: the read model catches up shortly after each command.</summary>
public sealed class ProjectionWorker(IProjector projector) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await projector.RunPendingAsync(stoppingToken);
    }
}
