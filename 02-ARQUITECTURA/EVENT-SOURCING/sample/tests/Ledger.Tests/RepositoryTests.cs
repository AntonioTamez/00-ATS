using Ledger.Domain;
using Ledger.Infrastructure;

namespace Ledger.Tests;

internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}

/// <summary>Wraps a store to see HOW MUCH of a stream a load really reads.</summary>
internal sealed class SpyStore(IEventStore inner) : IEventStore
{
    public List<(string Stream, int After, int Returned)> StreamReads { get; } = [];

    public Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<NewEvent> events, DateTimeOffset at, CancellationToken ct = default) =>
        inner.AppendAsync(streamId, expectedVersion, events, at, ct);

    public async Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string streamId, int afterVersion = 0, CancellationToken ct = default)
    {
        var result = await inner.ReadStreamAsync(streamId, afterVersion, ct);
        StreamReads.Add((streamId, afterVersion, result.Count));
        return result;
    }

    public Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long afterPosition = 0, CancellationToken ct = default) =>
        inner.ReadAllAsync(afterPosition, ct);
}

public class RepositoryTests
{
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryEventStore _store = new();
    private readonly InMemorySnapshotStore _snapshots = new();
    private readonly EventSerializer _serializer = EventSerializer.CreateDefault();

    private StockRepository Repository(IEventStore? store = null, int snapshotEvery = 5) =>
        new(store ?? _store, _snapshots, _serializer, _time, snapshotEvery);

    private StockService Service(int maxRetries = 3) => new(Repository(), maxRetries);

    // ---------- save and load ----------

    [Fact]
    public async Task ASavedItem_IsRebuiltFromItsEvents()
    {
        var repository = Repository();
        var item = StockItem.Register("book-1", "Clean Architecture");
        item.Receive(10, "Acme");
        await repository.SaveAsync(item);

        var loaded = await repository.LoadAsync("BOOK-1");

        Assert.Equal(("BOOK-1", "Clean Architecture", 10, 2), (loaded!.Sku, loaded.Name, loaded.OnHand, loaded.Version));
    }

    [Fact]
    public async Task OnlyEventsAreStored_NotTheState()
    {
        var repository = Repository();
        var item = StockItem.Register("A", "Item");
        item.Receive(5, null);
        await repository.SaveAsync(item);

        var stored = await _store.ReadAllAsync();

        Assert.Equal(["item-registered", "stock-received"], stored.Select(e => e.Type));
        Assert.DoesNotContain(stored, e => e.Type.Contains("snapshot"));
    }

    [Fact]
    public async Task AnUnknownItem_LoadsAsNull() =>
        Assert.Null(await Repository().LoadAsync("NOPE"));

    [Fact]
    public async Task SavingWithoutNewEvents_WritesNothing()
    {
        var repository = Repository();
        var item = StockItem.Register("A", "Item");
        await repository.SaveAsync(item);

        await repository.SaveAsync(item);

        Assert.Single(await _store.ReadAllAsync());
    }

    [Fact]
    public async Task EventsAreStamped_WithTheTimeOfTheSave()
    {
        var repository = Repository();
        var item = StockItem.Register("A", "Item");
        await repository.SaveAsync(item);

        Assert.Equal(_time.GetUtcNow(), (await _store.ReadAllAsync()).Single().OccurredAt);
    }

    // ---------- snapshots ----------

    [Fact]
    public async Task ASnapshot_IsTakenEveryFifthEvent()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");                            // 1 event
        for (var i = 0; i < 3; i++) await service.ReceiveAsync("A", 1, null);   // up to 4 events
        Assert.Null(await _snapshots.TryGetAsync("stock-A"));

        await service.ReceiveAsync("A", 1, null);                            // the 5th event

        var snapshot = await _snapshots.TryGetAsync("stock-A");
        Assert.Equal((5, 4), (snapshot!.Version, snapshot.OnHand));
    }

    [Fact]
    public async Task Loading_WithASnapshot_ReadsOnlyTheEventsAfterIt()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        for (var i = 0; i < 6; i++) await service.ReceiveAsync("A", 1, null);   // 7 events, snapshot at 5
        var spy = new SpyStore(_store);

        var loaded = await Repository(spy).LoadAsync("A");

        var read = Assert.Single(spy.StreamReads);
        Assert.Equal((5, 2), (read.After, read.Returned));                    // read from version 5 on: just 2 events
        Assert.Equal((7, 6), (loaded!.Version, loaded.OnHand));
    }

    [Fact]
    public async Task LoadingWithOrWithoutSnapshots_GivesTheSameState()
    {
        var withSnapshots = Service();
        await withSnapshots.RegisterAsync("A", "Item");
        for (var i = 1; i <= 12; i++)
        {
            await withSnapshots.ReceiveAsync("A", i, "S");
            if (i % 3 == 0) await withSnapshots.ShipAsync("A", 1, $"R{i}");
        }

        var viaSnapshot = (await Repository().LoadAsync("A"))!.ToSnapshot();
        var viaFullReplay = StockItem.Rehydrate((await _store.ReadStreamAsync("stock-A")).Select(_serializer.Deserialize)).ToSnapshot();

        Assert.Equal(viaFullReplay, viaSnapshot);
    }

    [Fact]
    public async Task ASnapshot_CanBeDeleted_AndTheStateIsRebuiltFromTheEvents()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        for (var i = 0; i < 6; i++) await service.ReceiveAsync("A", 1, null);
        var expected = (await Repository().LoadAsync("A"))!.ToSnapshot();

        var withoutSnapshots = new StockRepository(_store, new InMemorySnapshotStore(), _serializer, _time);
        var rebuilt = (await withoutSnapshots.LoadAsync("A"))!.ToSnapshot();

        Assert.Equal(expected, rebuilt);
    }

    // ---------- optimistic concurrency ----------

    [Fact]
    public async Task TwoWritersFromTheSameVersion_OnlyTheFirstWins()
    {
        var repository = Repository();
        var seed = StockItem.Register("A", "Item");
        seed.Receive(10, null);
        await repository.SaveAsync(seed);
        var first = (await repository.LoadAsync("A"))!;
        var second = (await repository.LoadAsync("A"))!;

        first.Ship(4, "R1");
        second.Ship(3, "R2");
        await repository.SaveAsync(first);

        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.SaveAsync(second));
        var final = await repository.LoadAsync("A");
        Assert.Equal(6, final!.OnHand);                                    // only the first shipment exists
    }

    [Fact]
    public async Task AConflict_IsRetried_ByReloadingAndDecidingAgain()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        await service.ReceiveAsync("A", 10, null);
        // Another writer receives 5 more units right after we loaded but before we append.
        _store.BeforeNextAppend = _ => Repository().SaveAsync(Received(5)).GetAwaiter().GetResult();

        await service.ShipAsync("A", 4, "R1");                             // the first append is rejected; the retry succeeds

        Assert.Equal(11, (await Repository().LoadAsync("A"))!.OnHand);     // 10 + 5 (the other writer) - 4 (ours)
        Assert.Equal(["item-registered", "stock-received", "stock-received", "stock-shipped"],
            (await _store.ReadStreamAsync("stock-A")).Select(e => e.Type));
    }

    [Fact]
    public async Task AfterAConflict_TheRulesAreEvaluatedAgainstTheNewState()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        await service.ReceiveAsync("A", 5, null);
        // Another writer ships 3 first, leaving 2.
        _store.BeforeNextAppend = _ => Repository().SaveAsync(Shipped(3)).GetAwaiter().GetResult();

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ShipAsync("A", 4, "LATE"));

        Assert.Contains("only 2 on hand", ex.Message);                      // refused on retry, not blindly written
        Assert.Equal(2, (await Repository().LoadAsync("A"))!.OnHand);
    }

    [Fact]
    public async Task WithoutRetries_AConflictSurfaces()
    {
        var service = Service(maxRetries: 0);
        await service.RegisterAsync("A", "Item");
        await service.ReceiveAsync("A", 5, null);
        _store.BeforeNextAppend = _ => Repository().SaveAsync(Received(1)).GetAwaiter().GetResult();

        await Assert.ThrowsAsync<ConcurrencyException>(() => service.ShipAsync("A", 1, "R"));
    }

    private StockItem Received(int quantity)
    {
        var item = Repository().LoadAsync("A").GetAwaiter().GetResult()!;
        item.Receive(quantity, "OTHER");
        return item;
    }

    private StockItem Shipped(int quantity)
    {
        var item = Repository().LoadAsync("A").GetAwaiter().GetResult()!;
        item.Ship(quantity, "OTHER");
        return item;
    }

    // ---------- the service ----------

    [Fact]
    public async Task RegisteringTheSameItemTwice_IsRefused()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync("a", "Item again"));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public async Task CommandsOnAnUnknownItem_AreNotFound()
    {
        var service = Service();

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReceiveAsync("NOPE", 1, null));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ShipAsync("NOPE", 1, "R"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AdjustAsync("NOPE", 1, "r"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DiscontinueAsync("NOPE"));
    }

    [Fact]
    public async Task ARefusedCommand_LeavesNoTraceInTheStore()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        var before = (await _store.ReadAllAsync()).Count;

        await Assert.ThrowsAsync<DomainException>(() => service.ShipAsync("A", 1, "R"));

        Assert.Equal(before, (await _store.ReadAllAsync()).Count);
    }

    // ---------- time travel ----------

    [Fact]
    public async Task TheStateAtAPastMoment_IsRebuiltFromTheEventsUpToThen()
    {
        var service = Service();
        var t0 = _time.GetUtcNow();
        await service.RegisterAsync("A", "Item");
        _time.Advance(TimeSpan.FromHours(1));
        var t1 = _time.GetUtcNow();
        await service.ReceiveAsync("A", 10, null);
        _time.Advance(TimeSpan.FromHours(1));
        var t2 = _time.GetUtcNow();
        await service.ShipAsync("A", 4, "R1");
        _time.Advance(TimeSpan.FromHours(1));
        await service.ReceiveAsync("A", 5, null);
        var repository = Repository();

        Assert.Equal(0, (await repository.LoadAsOfAsync("A", t0))!.OnHand);
        Assert.Equal(10, (await repository.LoadAsOfAsync("A", t1))!.OnHand);
        Assert.Equal(10, (await repository.LoadAsOfAsync("A", t1.AddMinutes(30)))!.OnHand);
        Assert.Equal(6, (await repository.LoadAsOfAsync("A", t2))!.OnHand);
        Assert.Equal(11, (await repository.LoadAsync("A"))!.OnHand);                 // "now"
    }

    [Fact]
    public async Task BeforeTheItemExisted_ThereIsNoState()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");

        Assert.Null(await Repository().LoadAsOfAsync("A", _time.GetUtcNow().AddDays(-1)));
    }

    [Fact]
    public async Task TheHistory_IsTheAuditTrail_InOrder()
    {
        var service = Service();
        await service.RegisterAsync("A", "Item");
        await service.ReceiveAsync("A", 10, "Acme");
        await service.AdjustAsync("A", -2, "damaged");

        var history = await Repository().HistoryAsync("A");

        Assert.Equal(["item-registered", "stock-received", "stock-adjusted"], history.Select(e => e.Type));
        Assert.Contains("damaged", history[2].Data);
    }
}
