using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Ordering.Contracts;
using Shop.SharedKernel;

namespace Notifications.Core;

/// <summary>The ONLY public type of this assembly: the module's entry point for the host.</summary>
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<NotificationLog>();
        services.AddScoped<INotificationsApi, NotificationsApi>();

        // Subscriptions: this module decides what it listens to; the publisher knows nothing about it.
        services.AddScoped<IIntegrationEventHandler<OrderPlaced>, SendOrderConfirmation>();
        services.AddScoped<IIntegrationEventHandler<OrderCancelled>, SendCancellationNotice>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications", async (string email, INotificationsApi api, CancellationToken ct) =>
            Results.Ok(await api.ListForAsync(email, ct)));
        return app;
    }
}

internal sealed class NotificationLog
{
    private readonly ConcurrentQueue<NotificationDto> _sent = new();

    public void Add(NotificationDto notification) => _sent.Enqueue(notification);

    public IReadOnlyList<NotificationDto> For(string email) =>
        _sent.Where(n => string.Equals(n.To, email, StringComparison.OrdinalIgnoreCase)).ToList();
}

internal sealed class NotificationsApi(NotificationLog log) : INotificationsApi
{
    public Task<IReadOnlyList<NotificationDto>> ListForAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(log.For(email));
}

internal sealed class SendOrderConfirmation(NotificationLog log) : IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced e, CancellationToken cancellationToken)
    {
        log.Add(new NotificationDto(e.OccurredAt, e.CustomerEmail, $"Order {e.OrderId} confirmed. Total: {e.Total:0.00}"));
        return Task.CompletedTask;
    }
}

internal sealed class SendCancellationNotice(NotificationLog log) : IIntegrationEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled e, CancellationToken cancellationToken)
    {
        log.Add(new NotificationDto(e.OccurredAt, e.CustomerEmail, $"Order {e.OrderId} cancelled."));
        return Task.CompletedTask;
    }
}
