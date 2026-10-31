using Ledger.Domain;

namespace Ledger.Infrastructure;

/// <summary>Loads and saves an event-sourced aggregate: what is stored is its EVENTS, never its state.</summary>
public sealed class StockRepository(
    IEventStore store, ISnapshotStore snapshots, EventSerializer serializer, TimeProvider clock, int snapshotEvery = 5)
{
    public static string StreamId(string sku) => $"stock-{sku.Trim().ToUpperInvariant()}";

    /// <summary>Rebuilds the current state: latest snapshot (if any) + only the events after it.</summary>
    public async Task<StockItem?> LoadAsync(string sku, CancellationToken cancellationToken = default)
    {
        var streamId = StreamId(sku);
        var snapshot = await snapshots.TryGetAsync(streamId, cancellationToken);
        var tail = await store.ReadStreamAsync(streamId, snapshot?.Version ?? 0, cancellationToken);

        if (snapshot is null && tail.Count == 0) return null;

        var events = tail.Select(serializer.Deserialize);
        return snapshot is null ? StockItem.Rehydrate(events) : StockItem.Rehydrate(snapshot, events);
    }

    /// <summary>
    /// Appends the item's new events, expecting the stream to be exactly where this item was loaded.
    /// If somebody else wrote in the meantime, nothing is written and a ConcurrencyException is thrown.
    /// </summary>
    public async Task SaveAsync(StockItem item, CancellationToken cancellationToken = default)
    {
        var uncommitted = item.PullUncommitted();
        if (uncommitted.Count == 0) return;

        var expectedVersion = item.Version - uncommitted.Count;
        await store.AppendAsync(StreamId(item.Sku), expectedVersion,
            uncommitted.Select(serializer.Serialize).ToList(), clock.GetUtcNow(), cancellationToken);

        // Snapshot whenever this save crossed a multiple of snapshotEvery. An optimization only.
        if (snapshotEvery > 0 && item.Version / snapshotEvery > expectedVersion / snapshotEvery)
            await snapshots.SaveAsync(StreamId(item.Sku), item.ToSnapshot(), cancellationToken);
    }

    /// <summary>The state at a past moment: replay only the events recorded up to <paramref name="at"/>. Ignores snapshots.</summary>
    public async Task<StockItem?> LoadAsOfAsync(string sku, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var history = await store.ReadStreamAsync(StreamId(sku), 0, cancellationToken);
        var untilThen = history.Where(e => e.OccurredAt <= at).Select(serializer.Deserialize).ToList();
        return untilThen.Count == 0 ? null : StockItem.Rehydrate(untilThen);
    }

    public Task<IReadOnlyList<StoredEvent>> HistoryAsync(string sku, CancellationToken cancellationToken = default) =>
        store.ReadStreamAsync(StreamId(sku), 0, cancellationToken);
}

/// <summary>The commands. Each one: load the aggregate, let it decide, save the events it raised.</summary>
public sealed class StockService(StockRepository repository, int maxRetries = 3)
{
    public async Task RegisterAsync(string sku, string name, CancellationToken cancellationToken = default)
    {
        var item = StockItem.Register(sku, name);
        try
        {
            await repository.SaveAsync(item, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            throw new DomainException($"Item {item.Sku} is already registered.");
        }
    }

    public Task ReceiveAsync(string sku, int quantity, string? supplier, CancellationToken ct = default) =>
        ExecuteAsync(sku, i => i.Receive(quantity, supplier), ct);

    public Task ShipAsync(string sku, int quantity, string reference, CancellationToken ct = default) =>
        ExecuteAsync(sku, i => i.Ship(quantity, reference), ct);

    public Task AdjustAsync(string sku, int delta, string reason, CancellationToken ct = default) =>
        ExecuteAsync(sku, i => i.Adjust(delta, reason), ct);

    public Task DiscontinueAsync(string sku, CancellationToken ct = default) =>
        ExecuteAsync(sku, i => i.Discontinue(), ct);

    /// <summary>
    /// If another writer got there first, the append is rejected; we reload the NEW state and decide again
    /// (a shipment valid a moment ago may no longer be valid). That re-decision is the point of retrying.
    /// </summary>
    private async Task ExecuteAsync(string sku, Action<StockItem> command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var item = await repository.LoadAsync(sku, cancellationToken)
                ?? throw new NotFoundException($"Item {sku} was not found.");

            command(item);                                   // the domain may refuse here

            try
            {
                await repository.SaveAsync(item, cancellationToken);
                return;
            }
            catch (ConcurrencyException) when (attempt < maxRetries)
            {
                // reload and decide again
            }
        }
    }
}
