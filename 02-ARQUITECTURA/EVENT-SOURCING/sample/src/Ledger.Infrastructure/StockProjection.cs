using Ledger.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ledger.Infrastructure;

public sealed record StockSummary(string Sku, string Name, int OnHand, bool Discontinued);

public sealed record MovementItem(long Position, DateTimeOffset At, string Kind, int Delta, string? Detail, int BalanceAfter);

/// <summary>
/// A read model built by replaying ALL events in global order. It is disposable: delete it and
/// <see cref="RebuildAsync"/> recreates it from the events. It resumes from its checkpoint, so catching up twice applies nothing twice.
/// </summary>
public sealed class StockProjection(IEventStore store, EventSerializer serializer)
{
    private sealed class Row
    {
        public string Sku = "";
        public string Name = "";
        public int OnHand;
        public bool Discontinued;
        public readonly List<MovementItem> Movements = [];
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Row> _rows = new(StringComparer.OrdinalIgnoreCase);
    private long _checkpoint;

    public long Checkpoint
    {
        get { lock (_gate) return _checkpoint; }
    }

    /// <returns>How many events were applied.</returns>
    public async Task<int> CatchUpAsync(CancellationToken cancellationToken = default)
    {
        var pending = await store.ReadAllAsync(Checkpoint, cancellationToken);
        lock (_gate)
        {
            foreach (var stored in pending.Where(e => e.Position > _checkpoint))
            {
                Apply(stored, serializer.Deserialize(stored));
                _checkpoint = stored.Position;
            }
        }
        return pending.Count;
    }

    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _rows.Clear();
            _checkpoint = 0;
        }
        return await CatchUpAsync(cancellationToken);
    }

    public StockSummary? Get(string sku)
    {
        lock (_gate) return _rows.TryGetValue(sku, out var r) ? new StockSummary(r.Sku, r.Name, r.OnHand, r.Discontinued) : null;
    }

    public IReadOnlyList<StockSummary> List()
    {
        lock (_gate)
            return _rows.Values.OrderBy(r => r.Sku).Select(r => new StockSummary(r.Sku, r.Name, r.OnHand, r.Discontinued)).ToList();
    }

    /// <returns>null when the item is unknown; otherwise its movements, newest first.</returns>
    public IReadOnlyList<MovementItem>? Movements(string sku)
    {
        lock (_gate) return _rows.TryGetValue(sku, out var r) ? r.Movements.AsEnumerable().Reverse().ToList() : null;
    }

    private void Apply(StoredEvent stored, IDomainEvent e)
    {
        if (e is ItemRegistered registered)
        {
            _rows[registered.Sku] = new Row { Sku = registered.Sku, Name = registered.Name };
            return;
        }

        var sku = stored.StreamId["stock-".Length..];
        if (!_rows.TryGetValue(sku, out var row)) return;

        switch (e)
        {
            case StockReceived r: Move(row, stored, "Received", r.Quantity, r.Supplier); break;
            case StockShipped s: Move(row, stored, "Shipped", -s.Quantity, s.Reference); break;
            case StockAdjusted a: Move(row, stored, "Adjusted", a.Delta, a.Reason); break;
            case ItemDiscontinued: row.Discontinued = true; break;
        }
    }

    private static void Move(Row row, StoredEvent stored, string kind, int delta, string? detail)
    {
        row.OnHand += delta;
        row.Movements.Add(new MovementItem(stored.Position, stored.OccurredAt, kind, delta, detail, row.OnHand));
    }
}

public static class LedgerRegistration
{
    public static IServiceCollection AddLedger(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InMemoryEventStore>();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<InMemoryEventStore>());
        services.AddSingleton<ISnapshotStore, InMemorySnapshotStore>();
        services.AddSingleton(EventSerializer.CreateDefault());
        services.AddSingleton(sp => new StockRepository(
            sp.GetRequiredService<IEventStore>(), sp.GetRequiredService<ISnapshotStore>(),
            sp.GetRequiredService<EventSerializer>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new StockService(sp.GetRequiredService<StockRepository>()));
        services.AddSingleton<StockProjection>();
        return services;
    }
}
