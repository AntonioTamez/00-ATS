using System.Collections.Concurrent;
using Catalog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Contracts;
using Shop.SharedKernel;

namespace Ordering.Core;

/// <summary>The ONLY public type of this assembly: the module's entry point for the host.</summary>
public static class OrderingModule
{
    /// <summary>Needs <see cref="ICatalogApi"/> and <see cref="IEventBus"/> to be registered by the host.</summary>
    public static IServiceCollection AddOrderingModule(this IServiceCollection services)
    {
        services.AddSingleton<OrderStore>();
        services.AddScoped<IOrderingApi, OrderingApi>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", async (PlaceOrderCommand command, IOrderingApi orders, CancellationToken ct) =>
        {
            var result = await orders.PlaceAsync(command, ct);
            return result.Success
                ? Results.Created($"/orders/{result.Order!.Id}", result.Order)
                : Results.UnprocessableEntity(new { error = result.Error });
        });

        group.MapGet("/{id:guid}", async (Guid id, IOrderingApi orders, CancellationToken ct) =>
            await orders.GetAsync(id, ct) is { } order ? Results.Ok(order) : Results.NotFound());

        group.MapPost("/{id:guid}/cancel", async (Guid id, IOrderingApi orders, CancellationToken ct) =>
            await orders.CancelAsync(id, ct) switch
            {
                CancelOrderOutcome.Cancelled => Results.NoContent(),
                CancelOrderOutcome.NotFound => Results.NotFound(),
                _ => Results.Conflict(new { error = "The order is already cancelled." }),
            });

        return app;
    }
}

internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Save(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);
}

internal sealed class OrderingApi(ICatalogApi catalog, OrderStore store, IEventBus bus, TimeProvider clock) : IOrderingApi
{
    public async Task<PlaceOrderResult> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        var requested = (command.Lines ?? []).Select(l => (l.Sku, l.Quantity)).ToList();
        if (Order.Validate(command.CustomerEmail, requested) is { } invalid)
            return PlaceOrderResult.Fail(invalid);

        // Cross-module work goes through the other module's public contract.
        var reserved = new List<OrderLine>();
        foreach (var (sku, quantity) in requested)
        {
            var reservation = await catalog.ReserveAsync(sku, quantity, cancellationToken);
            if (!reservation.Success)
            {
                // No distributed transaction exists between modules: we compensate by hand.
                foreach (var done in reserved)
                    await catalog.ReleaseAsync(done.Sku, done.Quantity, cancellationToken);
                return PlaceOrderResult.Fail(reservation.Error!);
            }

            reserved.Add(new OrderLine(sku, quantity, reservation.UnitPrice));
        }

        var order = Order.Place(command.CustomerEmail, reserved, clock.GetUtcNow());
        store.Save(order);

        await bus.PublishAsync(
            new OrderPlaced(Guid.NewGuid(), clock.GetUtcNow(), order.Id, order.CustomerEmail, order.Total),
            cancellationToken);

        return PlaceOrderResult.Ok(ToDto(order));
    }

    public Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Find(orderId) is { } order ? ToDto(order) : null);

    public async Task<CancelOrderOutcome> CancelAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = store.Find(orderId);
        if (order is null) return CancelOrderOutcome.NotFound;
        if (!order.Cancel()) return CancelOrderOutcome.AlreadyCancelled;

        foreach (var line in order.Lines)
            await catalog.ReleaseAsync(line.Sku, line.Quantity, cancellationToken);

        await bus.PublishAsync(
            new OrderCancelled(Guid.NewGuid(), clock.GetUtcNow(), order.Id, order.CustomerEmail),
            cancellationToken);

        return CancelOrderOutcome.Cancelled;
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CustomerEmail, o.Status.ToString(), o.Total,
        o.Lines.Select(l => new OrderLineDto(l.Sku, l.Quantity, l.UnitPrice)).ToList());
}
