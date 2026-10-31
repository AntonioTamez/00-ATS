namespace Faas.Runtime;

public sealed record DeadLetter(QueueMessage Message, string Reason);

/// <summary>
/// A queue service with at-least-once semantics: a message that is not completed comes back with its delivery
/// count increased. Sending the same message id twice delivers it twice (the platform does not deduplicate).
/// </summary>
public sealed class InMemoryMessageBus
{
    private readonly Dictionary<string, Queue<QueueMessage>> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DeadLetter>> _dead = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private int _nextId;

    public string Send(string queue, string body, string? messageId = null)
    {
        lock (_gate)
        {
            var id = messageId ?? $"msg-{++_nextId}";
            QueueOf(queue).Enqueue(new QueueMessage(id, queue, body, 1));
            return id;
        }
    }

    /// <summary>Takes up to <paramref name="max"/> messages out of the queue.</summary>
    public IReadOnlyList<QueueMessage> Receive(string queue, int max)
    {
        lock (_gate)
        {
            var q = QueueOf(queue);
            var batch = new List<QueueMessage>();
            while (batch.Count < max && q.TryDequeue(out var message))
                batch.Add(message);
            return batch;
        }
    }

    /// <summary>The function failed: the message goes back to the end of the queue with one more delivery.</summary>
    public void Abandon(QueueMessage message)
    {
        lock (_gate)
            QueueOf(message.Queue).Enqueue(message with { DeliveryCount = message.DeliveryCount + 1 });
    }

    public void SendToDeadLetter(QueueMessage message, string reason)
    {
        lock (_gate)
        {
            if (!_dead.TryGetValue(message.Queue, out var list))
                _dead[message.Queue] = list = [];
            list.Add(new DeadLetter(message, reason));
        }
    }

    public int Pending(string queue)
    {
        lock (_gate)
            return QueueOf(queue).Count;
    }

    /// <summary>The messages waiting in a queue, without taking them.</summary>
    public IReadOnlyList<QueueMessage> Peek(string queue)
    {
        lock (_gate)
            return [.. QueueOf(queue)];
    }

    public IReadOnlyList<DeadLetter> DeadLetters(string queue)
    {
        lock (_gate)
            return _dead.TryGetValue(queue, out var list) ? [.. list] : [];
    }

    private Queue<QueueMessage> QueueOf(string queue)
    {
        if (!_queues.TryGetValue(queue, out var q))
            _queues[queue] = q = new Queue<QueueMessage>();
        return q;
    }
}
