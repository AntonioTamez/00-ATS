using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eda.Messaging;

/// <summary>
/// Messages a participant decided to publish, stored next to its own data. Saving the state change and adding
/// the message happen inside <see cref="Atomically"/> (in a real database: the same transaction), so a change
/// can never be saved without its message, nor a message sent for a change that was not saved.
/// </summary>
public abstract class Outbox
{
    private readonly object _gate = new();
    private readonly List<Message> _pending = [];

    public void Atomically(Action work)
    {
        lock (_gate) work();
    }

    public void Add(Message message)
    {
        lock (_gate) _pending.Add(message);
    }

    public IReadOnlyList<Message> Pending()
    {
        lock (_gate) return _pending.ToList();
    }

    internal void MarkSent(Guid messageId)
    {
        lock (_gate) _pending.RemoveAll(m => m.Id == messageId);
    }
}

/// <summary>
/// Moves messages from an outbox to the broker. If the broker is down it stops and leaves them pending.
/// If the process dies after publishing but before marking, the message is published again: duplicates are possible.
/// </summary>
public sealed class OutboxRelay(Outbox outbox, IMessageBus bus)
{
    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        foreach (var message in outbox.Pending())
        {
            try
            {
                await bus.PublishAsync(message, cancellationToken);
            }
            catch (Exception)
            {
                break;   // keep the rest (and this one) pending; try again on the next round
            }

            outbox.MarkSent(message.Id);
            published++;
        }
        return published;
    }
}

/// <summary>The consumer's record of messages it already handled, so a duplicate does nothing.</summary>
public abstract class Inbox
{
    private readonly object _gate = new();
    private readonly HashSet<Guid> _processed = [];

    public int Count
    {
        get { lock (_gate) return _processed.Count; }
    }

    /// <returns>false when the message had already been processed (nothing was done).</returns>
    public async Task<bool> ProcessOnceAsync(Guid messageId, Func<Task> work)
    {
        lock (_gate)
            if (_processed.Contains(messageId)) return false;

        await work();                                  // if this throws the message is NOT marked: the retry will run it again

        lock (_gate) _processed.Add(messageId);
        return true;
    }
}

public static class SubscriptionExtensions
{
    /// <summary>Subscribes with duplicate protection: the handler runs at most once per message id.</summary>
    public static void SubscribeOnce(this ISubscriptions subscriptions, string subscriber, string messageType, Inbox inbox,
        Func<Message, CancellationToken, Task> handler) =>
        subscriptions.Subscribe(subscriber, messageType,
            (message, ct) => inbox.ProcessOnceAsync(message.Id, () => handler(message, ct)));
}

public interface IPump
{
    /// <summary>Relays every outbox and delivers messages until nothing moves any more.</summary>
    /// <returns>How many things moved (messages relayed + deliveries attempted).</returns>
    Task<int> RunUntilIdleAsync(CancellationToken cancellationToken = default);
}

public sealed class Pump(IEnumerable<OutboxRelay> relays, InMemoryBroker broker) : IPump
{
    private const int MaxRounds = 200;
    private readonly OutboxRelay[] _relays = relays.ToArray();

    public async Task<int> RunUntilIdleAsync(CancellationToken cancellationToken = default)
    {
        var total = 0;
        for (var round = 0; round < MaxRounds; round++)
        {
            var moved = 0;
            foreach (var relay in _relays) moved += await relay.PublishPendingAsync(cancellationToken);
            moved += await broker.DeliverRoundAsync(cancellationToken);

            if (moved == 0) break;
            total += moved;
        }
        return total;
    }
}

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InMemoryBroker>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<InMemoryBroker>());
        services.AddSingleton<ISubscriptions>(sp => sp.GetRequiredService<InMemoryBroker>());
        services.AddSingleton<IPump, Pump>();
        return services;
    }

    /// <summary>Asks every participant to register its subscriptions. Call once, when the app starts.</summary>
    public static IServiceProvider ActivateSubscribers(this IServiceProvider services)
    {
        var subscriptions = services.GetRequiredService<ISubscriptions>();
        foreach (var subscriber in services.GetServices<ISubscriber>())
            subscriber.Subscribe(subscriptions);
        return services;
    }
}
