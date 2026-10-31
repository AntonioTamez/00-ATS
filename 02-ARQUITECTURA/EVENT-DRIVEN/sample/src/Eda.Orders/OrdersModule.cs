using System.Collections.Concurrent;
using Eda.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Eda.Orders;

/// <summary>The ONLY public type of this assembly: the participant's entry point for the host.</summary>
public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddSingleton<OrderStore>();
        services.AddSingleton<OrdersOutbox>();
        services.AddSingleton<OrdersInbox>();
        services.AddSingleton(sp => new OutboxRelay(sp.GetRequiredService<OrdersOutbox>(), sp.GetRequiredService<IMessageBus>()));
        services.AddSingleton<ISubscriber, OrdersSubscriber>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", (PlaceOrderRequest request, OrderStore store, OrdersOutbox outbox, TimeProvider clock) =>
        {
            if (Order.Validate(request.CustomerEmail, request.Sku, request.Quantity) is { } invalid)
                return Results.UnprocessableEntity(new { error = invalid });

            var order = Order.Place(request.CustomerEmail!, request.Sku!, request.Quantity);

            // State + message in ONE atomic step. Nothing is sent to anybody here: that happens later, through the relay.
            outbox.Atomically(() =>
            {
                store.Save(order);
                outbox.Add(Message.Create(OrdersMessages.OrderPlaced,
                    new { orderId = order.Id, customerEmail = order.CustomerEmail, sku = order.Sku, quantity = order.Quantity },
                    order.Id.ToString(), clock.GetUtcNow()));
            });

            // 202 Accepted: "I took your order and will process it", not "it is done".
            return Results.Accepted($"/orders/{order.Id}", new { id = order.Id, status = order.Status.ToString() });
        });

        app.MapGet("/orders/{id:guid}", (Guid id, OrderStore store) =>
            store.Find(id) is { } o
                ? Results.Ok(new { id = o.Id, status = o.Status.ToString(), o.CustomerEmail, o.Sku, o.Quantity, reason = o.RejectionReason })
                : Results.NotFound());

        app.MapPost("/orders/{id:guid}/cancel", (Guid id, OrderStore store, OrdersOutbox outbox, TimeProvider clock) =>
        {
            var order = store.Find(id);
            if (order is null) return Results.NotFound();

            var cancelled = false;
            outbox.Atomically(() =>
            {
                if (!order.Cancel()) return;
                cancelled = true;
                outbox.Add(Message.Create(OrdersMessages.OrderCancelled,
                    new { orderId = order.Id, customerEmail = order.CustomerEmail },
                    order.Id.ToString(), clock.GetUtcNow()));
            });

            return cancelled
                ? Results.NoContent()
                : Results.Conflict(new { error = "Only a confirmed order can be cancelled." });
        });

        return app;
    }
}

internal sealed record PlaceOrderRequest(string? CustomerEmail, string? Sku, int Quantity);

/// <summary>The message types THIS participant publishes. Consumers write the same strings in their own code.</summary>
internal static class OrdersMessages
{
    public const string OrderPlaced = "orders.order-placed.v1";
    public const string OrderConfirmed = "orders.order-confirmed.v1";
    public const string OrderRejected = "orders.order-rejected.v1";
    public const string OrderCancelled = "orders.order-cancelled.v1";
}

internal sealed class OrdersOutbox : Outbox;

internal sealed class OrdersInbox : Inbox;

/// <summary>This participant's own data. Nobody else can read it.</summary>
internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Save(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);
}

/// <summary>Orders' own copy of the messages it consumes (from Inventory). Only the fields it needs.</summary>
internal sealed record StockReservedMessage(Guid OrderId);

internal sealed record StockRejectedMessage(Guid OrderId, string? Reason);

internal sealed class OrdersSubscriber(OrderStore store, OrdersOutbox outbox, OrdersInbox inbox, TimeProvider clock) : ISubscriber
{
    private const string StockReserved = "inventory.stock-reserved.v1";
    private const string StockRejected = "inventory.stock-rejected.v1";

    public void Subscribe(ISubscriptions subscriptions)
    {
        subscriptions.SubscribeOnce("orders", StockReserved, inbox, OnStockReserved);
        subscriptions.SubscribeOnce("orders", StockRejected, inbox, OnStockRejected);
    }

    private Task OnStockReserved(Message message, CancellationToken ct)
    {
        var body = message.Read<StockReservedMessage>();
        outbox.Atomically(() =>
        {
            var order = store.Find(body.OrderId);
            if (order is null || !order.Confirm()) return;     // unknown or already decided: ignore

            outbox.Add(Message.Create(OrdersMessages.OrderConfirmed,
                new { orderId = order.Id, customerEmail = order.CustomerEmail },
                order.Id.ToString(), clock.GetUtcNow()));
        });
        return Task.CompletedTask;
    }

    private Task OnStockRejected(Message message, CancellationToken ct)
    {
        var body = message.Read<StockRejectedMessage>();
        outbox.Atomically(() =>
        {
            var order = store.Find(body.OrderId);
            if (order is null || !order.Reject(body.Reason ?? "Rejected by inventory.")) return;

            outbox.Add(Message.Create(OrdersMessages.OrderRejected,
                new { orderId = order.Id, customerEmail = order.CustomerEmail, reason = order.RejectionReason },
                order.Id.ToString(), clock.GetUtcNow()));
        });
        return Task.CompletedTask;
    }
}
