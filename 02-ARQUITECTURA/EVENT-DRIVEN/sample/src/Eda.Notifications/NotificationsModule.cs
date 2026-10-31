using System.Collections.Concurrent;
using Eda.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Eda.Notifications;

/// <summary>The ONLY public type of this assembly: the participant's entry point for the host.</summary>
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<NotificationLog>();
        services.AddSingleton<NotificationsInbox>();
        services.AddSingleton<ISubscriber, NotificationsSubscriber>();
        // No outbox and no relay: this participant never publishes anything. It only listens.
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications", (string email, NotificationLog log) => Results.Ok(log.For(email)));
        return app;
    }
}

internal sealed class NotificationsInbox : Inbox;

internal sealed record Notification(DateTimeOffset At, string To, string Subject);

internal sealed class NotificationLog
{
    private readonly ConcurrentQueue<Notification> _sent = new();

    public void Add(Notification notification) => _sent.Enqueue(notification);

    public IReadOnlyList<Notification> For(string email) =>
        _sent.Where(n => string.Equals(n.To, email, StringComparison.OrdinalIgnoreCase)).ToList();
}

/// <summary>Notifications' own copy of the Orders messages it reacts to.</summary>
internal sealed record OrderEventMessage(Guid OrderId, string? CustomerEmail, string? Reason);

internal sealed class NotificationsSubscriber(NotificationLog log, NotificationsInbox inbox) : ISubscriber
{
    public void Subscribe(ISubscriptions subscriptions)
    {
        subscriptions.SubscribeOnce("notifications", "orders.order-confirmed.v1", inbox,
            (m, _) => Send(m, e => $"Your order {e.OrderId} is confirmed."));
        subscriptions.SubscribeOnce("notifications", "orders.order-rejected.v1", inbox,
            (m, _) => Send(m, e => $"Your order {e.OrderId} could not be completed: {e.Reason}"));
        subscriptions.SubscribeOnce("notifications", "orders.order-cancelled.v1", inbox,
            (m, _) => Send(m, e => $"Your order {e.OrderId} was cancelled."));
    }

    private Task Send(Message message, Func<OrderEventMessage, string> subject)
    {
        var body = message.Read<OrderEventMessage>();
        if (string.IsNullOrWhiteSpace(body.CustomerEmail)) return Task.CompletedTask;   // nobody to notify

        log.Add(new Notification(message.OccurredAt, body.CustomerEmail, subject(body)));
        return Task.CompletedTask;
    }
}
