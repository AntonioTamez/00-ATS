using Eda.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Eda.Inventory;

/// <summary>The ONLY public type of this assembly: the participant's entry point for the host.</summary>
public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddSingleton<StockStore>();
        services.AddSingleton<InventoryOutbox>();
        services.AddSingleton<InventoryInbox>();
        services.AddSingleton(sp => new OutboxRelay(sp.GetRequiredService<InventoryOutbox>(), sp.GetRequiredService<IMessageBus>()));
        services.AddSingleton<ISubscriber, InventorySubscriber>();
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory/{sku}", (string sku, StockStore stock) =>
            stock.Available(sku) is { } available ? Results.Ok(new { sku, available }) : Results.NotFound());
        return app;
    }
}

internal sealed class InventoryOutbox : Outbox;

internal sealed class InventoryInbox : Inbox;

/// <summary>This participant's own data: available stock, and what it reserved for each order.</summary>
internal sealed class StockStore
{
    private sealed record Reservation(string Sku, int Quantity) { public bool Released { get; set; } }

    private readonly object _gate = new();
    private readonly Dictionary<string, int> _available = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BOOK-1"] = 10,
        ["MUG-1"] = 5,
        ["PEN-1"] = 100,
    };
    private readonly Dictionary<Guid, Reservation> _byOrder = [];

    public int? Available(string sku)
    {
        lock (_gate) return _available.TryGetValue(sku, out var n) ? n : null;
    }

    /// <returns>null when reserved; otherwise the reason it could not be.</returns>
    public string? TryReserve(Guid orderId, string sku, int quantity)
    {
        lock (_gate)
        {
            if (!_available.TryGetValue(sku, out var available)) return $"Unknown product {sku}.";
            if (quantity <= 0) return "The quantity must be positive.";
            if (available < quantity) return $"Not enough stock for {sku}: requested {quantity}, available {available}.";

            _available[sku] = available - quantity;
            _byOrder[orderId] = new Reservation(sku, quantity);
            return null;
        }
    }

    public bool HasReservationFor(Guid orderId)
    {
        lock (_gate) return _byOrder.ContainsKey(orderId);
    }

    public void Release(Guid orderId)
    {
        lock (_gate)
        {
            if (!_byOrder.TryGetValue(orderId, out var reservation) || reservation.Released) return;
            reservation.Released = true;
            _available[reservation.Sku] += reservation.Quantity;
        }
    }
}

/// <summary>Inventory's own copy of what it consumes from Orders. Fields it does not use are ignored.</summary>
internal sealed record OrderPlacedMessage(Guid OrderId, string? Sku, int Quantity);

internal sealed record OrderCancelledMessage(Guid OrderId);

internal sealed class InventorySubscriber(StockStore stock, InventoryOutbox outbox, InventoryInbox inbox, TimeProvider clock) : ISubscriber
{
    private const string OrderPlaced = "orders.order-placed.v1";
    private const string OrderCancelled = "orders.order-cancelled.v1";
    private const string StockReserved = "inventory.stock-reserved.v1";
    private const string StockRejected = "inventory.stock-rejected.v1";

    public void Subscribe(ISubscriptions subscriptions)
    {
        subscriptions.SubscribeOnce("inventory", OrderPlaced, inbox, OnOrderPlaced);
        subscriptions.SubscribeOnce("inventory", OrderCancelled, inbox, OnOrderCancelled);
    }

    private Task OnOrderPlaced(Message message, CancellationToken ct)
    {
        var order = message.Read<OrderPlacedMessage>();

        outbox.Atomically(() =>
        {
            // Business-level guard, on top of the inbox: the same order is never reserved twice,
            // even if the message came again with a different id.
            if (stock.HasReservationFor(order.OrderId)) return;

            var reason = stock.TryReserve(order.OrderId, order.Sku ?? "", order.Quantity);
            outbox.Add(reason is null
                ? Message.Create(StockReserved, new { orderId = order.OrderId, sku = order.Sku, quantity = order.Quantity },
                    message.CorrelationId, clock.GetUtcNow())
                : Message.Create(StockRejected, new { orderId = order.OrderId, reason },
                    message.CorrelationId, clock.GetUtcNow()));
        });
        return Task.CompletedTask;
    }

    private Task OnOrderCancelled(Message message, CancellationToken ct)
    {
        stock.Release(message.Read<OrderCancelledMessage>().OrderId);
        return Task.CompletedTask;
    }
}
