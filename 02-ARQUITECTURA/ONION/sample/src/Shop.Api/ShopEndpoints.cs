using Shop.Application;

namespace Shop.Api;

public sealed record RegisterCustomerRequest(string Name);
public sealed record PlaceOrderRequest(Guid CustomerId, List<PlaceOrderLine> Lines);

/// <summary>UI ring: HTTP in, application service call, HTTP out. No business rules.</summary>
public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers", async (RegisterCustomerRequest body, RegisterCustomerService svc, CancellationToken ct) =>
        {
            var customer = await svc.ExecuteAsync(body.Name, ct);
            return Results.Created($"/api/customers/{customer.Id}", customer);
        });

        var orders = app.MapGroup("/api/orders");

        orders.MapPost("/", async (PlaceOrderRequest body, PlaceOrderService svc, CancellationToken ct) =>
        {
            var order = await svc.ExecuteAsync(new PlaceOrderCommand(body.CustomerId, body.Lines), ct);
            return Results.Created($"/api/orders/{order.Id}", order);
        });

        orders.MapGet("/{id:guid}", async (Guid id, GetOrderService svc, CancellationToken ct) =>
            Results.Ok(await svc.ExecuteAsync(id, ct)));

        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderService svc, CancellationToken ct) =>
            Results.Ok(await svc.ExecuteAsync(id, ct)));

        return app;
    }
}
