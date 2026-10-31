using Microsoft.Extensions.Logging;

namespace Eda.Messaging;

public delegate Task MessageHandler(Message message, CancellationToken cancellationToken);

public interface IMessageBus
{
    Task PublishAsync(Message message, CancellationToken cancellationToken = default);
}

public interface ISubscriptions
{
    void Subscribe(string subscriber, string messageType, MessageHandler handler);
}

/// <summary>A participant that wants to listen to messages. The host asks every one to subscribe at startup.</summary>
public interface ISubscriber
{
    void Subscribe(ISubscriptions subscriptions);
}

public sealed record DeadLetter(string Subscriber, Message Message, string Error, int Attempts);

/// <summary>
/// An in-memory stand-in for a message broker (RabbitMQ, Service Bus, Kafka...). It has the traits that matter:
/// one queue per subscriber, delivery AT LEAST ONCE (so duplicates are possible), retries, and a dead-letter list.
/// Delivery happens when <see cref="DeliverRoundAsync"/> is called, so tests decide when "later" is.
/// </summary>
public sealed class InMemoryBroker(ILogger<InMemoryBroker> logger) : IMessageBus, ISubscriptions
{
    public const int MaxAttempts = 3;

    private sealed record Delivery(Message Message, int Attempt);

    private sealed class Subscription
    {
        public Dictionary<string, MessageHandler> Handlers { get; } = [];
        public Queue<Delivery> Queue { get; } = new();
        public bool Paused { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Subscription> _subscriptions = [];
    private readonly List<DeadLetter> _deadLetters = [];

    /// <summary>Test switch: every message is delivered twice, like a broker that retries after a lost ack.</summary>
    public bool DuplicateDeliveries { get; set; }

    /// <summary>Test switch: the broker is unreachable, so publishing throws.</summary>
    public bool Offline { get; set; }

    public IReadOnlyList<DeadLetter> DeadLetters
    {
        get { lock (_gate) return _deadLetters.ToList(); }
    }

    public void Subscribe(string subscriber, string messageType, MessageHandler handler)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscriber, out var subscription))
                _subscriptions[subscriber] = subscription = new Subscription();
            subscription.Handlers[messageType] = handler;
        }
    }

    public Task PublishAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (Offline) throw new InvalidOperationException("The broker is unreachable.");

        lock (_gate)
        {
            foreach (var subscription in _subscriptions.Values.Where(s => s.Handlers.ContainsKey(message.Type)))
            {
                subscription.Queue.Enqueue(new Delivery(message, 0));
                if (DuplicateDeliveries) subscription.Queue.Enqueue(new Delivery(message, 0));
            }
        }
        return Task.CompletedTask;
    }

    public void Pause(string subscriber) => SetPaused(subscriber, true);

    public void Resume(string subscriber) => SetPaused(subscriber, false);

    public int Pending(string subscriber)
    {
        lock (_gate) return _subscriptions.TryGetValue(subscriber, out var s) ? s.Queue.Count : 0;
    }

    /// <summary>One delivery round: every subscriber handles what was waiting when the round began.</summary>
    /// <returns>How many deliveries were attempted.</returns>
    public async Task<int> DeliverRoundAsync(CancellationToken cancellationToken = default)
    {
        List<(string Name, Subscription Subscription)> snapshot;
        lock (_gate) snapshot = _subscriptions.Select(kv => (kv.Key, kv.Value)).ToList();

        var attempted = 0;
        foreach (var (name, subscription) in snapshot)
        {
            int waiting;
            lock (_gate) waiting = subscription.Paused ? 0 : subscription.Queue.Count;

            for (var i = 0; i < waiting; i++)
            {
                Delivery delivery;
                MessageHandler handler;
                lock (_gate)
                {
                    delivery = subscription.Queue.Dequeue();
                    handler = subscription.Handlers[delivery.Message.Type];
                }

                attempted++;
                try
                {
                    await handler(delivery.Message, cancellationToken);
                }
                catch (Exception ex)
                {
                    HandleFailure(name, subscription, delivery, ex);
                }
            }
        }
        return attempted;
    }

    private void HandleFailure(string subscriber, Subscription subscription, Delivery delivery, Exception error)
    {
        var attempts = delivery.Attempt + 1;
        lock (_gate)
        {
            if (attempts >= MaxAttempts)
            {
                _deadLetters.Add(new DeadLetter(subscriber, delivery.Message, error.Message, attempts));
                logger.LogError(error, "{Subscriber} gave up on {Type} {Id} after {Attempts} attempts: moved to the dead-letter list",
                    subscriber, delivery.Message.Type, delivery.Message.Id, attempts);
                return;
            }

            // The retry goes to the BACK of the queue: retries can reorder messages. Consumers must not depend on order.
            subscription.Queue.Enqueue(delivery with { Attempt = attempts });
        }
        logger.LogWarning(error, "{Subscriber} failed on {Type} {Id} (attempt {Attempt}); it will be retried",
            subscriber, delivery.Message.Type, delivery.Message.Id, attempts);
    }

    private void SetPaused(string subscriber, bool paused)
    {
        lock (_gate)
            if (_subscriptions.TryGetValue(subscriber, out var subscription))
                subscription.Paused = paused;
    }
}
