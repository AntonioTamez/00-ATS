using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shop.SharedKernel;

/// <summary>Marker for events that cross a module boundary. Published by one module, consumed by others.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// In-process bus. Handlers run synchronously, each in its own DI scope. A failing handler is logged and
/// does not break the publisher or the other handlers (there is no retry: see "Limits" in the guide).
/// </summary>
public sealed class InMemoryEventBus(IServiceScopeFactory scopes, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        using var scope = scopes.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handler {Handler} failed for {Event} {EventId}",
                    handler.GetType().Name, typeof(TEvent).Name, integrationEvent.EventId);
            }
        }
    }
}
