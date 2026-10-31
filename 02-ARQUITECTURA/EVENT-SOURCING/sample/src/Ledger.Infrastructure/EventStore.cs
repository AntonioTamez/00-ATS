using Ledger.Domain;

namespace Ledger.Infrastructure;

/// <summary>An event ready to be stored: its type name, the schema version it was written with, and its JSON.</summary>
public sealed record NewEvent(string Type, int SchemaVersion, string Data);

/// <summary>An event as it sits in the store. Immutable: nobody can update it.</summary>
public sealed record StoredEvent(
    string StreamId, int Version, long Position, string Type, int SchemaVersion, string Data, DateTimeOffset OccurredAt);

public sealed class ConcurrencyException(string streamId, int expected, int actual)
    : Exception($"Stream '{streamId}' is at version {actual}, but the writer expected {expected}.")
{
    public string StreamId { get; } = streamId;
}

public sealed class NotFoundException(string message) : Exception(message);

public interface IEventStore
{
    /// <summary>
    /// Appends events to a stream ONLY if the stream is still at <paramref name="expectedVersion"/>
    /// (optimistic concurrency). Otherwise nothing is written and a <see cref="ConcurrencyException"/> is thrown.
    /// </summary>
    Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<NewEvent> events, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>The events of ONE stream (one aggregate), in order, after the given version.</summary>
    Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string streamId, int afterVersion = 0,
        CancellationToken cancellationToken = default);

    /// <summary>The events of ALL streams in global order, after the given position. Projections read this.</summary>
    Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long afterPosition = 0, CancellationToken cancellationToken = default);
}

/// <summary>An append-only, in-memory event store. There is no Update and no Delete.</summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly object _gate = new();
    private readonly List<StoredEvent> _all = [];
    private readonly Dictionary<string, List<StoredEvent>> _streams = [];

    /// <summary>Test hook: runs once, right before the next append, to simulate another writer sneaking in.</summary>
    public Action<string>? BeforeNextAppend { get; set; }

    public Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<NewEvent> events, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var hook = BeforeNextAppend;
        BeforeNextAppend = null;
        hook?.Invoke(streamId);

        lock (_gate)
        {
            var stream = _streams.TryGetValue(streamId, out var existing) ? existing : _streams[streamId] = [];
            if (stream.Count != expectedVersion) throw new ConcurrencyException(streamId, expectedVersion, stream.Count);

            foreach (var e in events)
            {
                var stored = new StoredEvent(streamId, stream.Count + 1, _all.Count + 1, e.Type, e.SchemaVersion, e.Data, at);
                stream.Add(stored);
                _all.Add(stored);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string streamId, int afterVersion = 0,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StoredEvent> result = _streams.TryGetValue(streamId, out var stream)
                ? stream.Where(e => e.Version > afterVersion).ToList()
                : [];
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long afterPosition = 0, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<StoredEvent>>(_all.Where(e => e.Position > afterPosition).ToList());
    }
}

/// <summary>Where snapshots live. A snapshot can always be thrown away and rebuilt from the events.</summary>
public interface ISnapshotStore
{
    Task SaveAsync(string streamId, StockItemSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<StockItemSnapshot?> TryGetAsync(string streamId, CancellationToken cancellationToken = default);
}

public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly Dictionary<string, StockItemSnapshot> _snapshots = [];

    public Task SaveAsync(string streamId, StockItemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (_snapshots) _snapshots[streamId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<StockItemSnapshot?> TryGetAsync(string streamId, CancellationToken cancellationToken = default)
    {
        lock (_snapshots) return Task.FromResult(_snapshots.GetValueOrDefault(streamId));
    }
}
