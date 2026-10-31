using System.Text.Json.Nodes;
using Ledger.Domain;
using Ledger.Infrastructure;

namespace Ledger.Tests;

public class EventStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static NewEvent Event(string type = "t", string data = "{}") => new(type, 1, data);

    [Fact]
    public async Task Appending_AssignsConsecutiveVersions_PerStream()
    {
        var store = new InMemoryEventStore();

        await store.AppendAsync("a", 0, [Event(), Event()], T0);
        await store.AppendAsync("a", 2, [Event()], T0);

        var stream = await store.ReadStreamAsync("a");
        Assert.Equal([1, 2, 3], stream.Select(e => e.Version));
    }

    [Fact]
    public async Task Positions_AreGlobal_AcrossStreams()
    {
        var store = new InMemoryEventStore();

        await store.AppendAsync("a", 0, [Event()], T0);
        await store.AppendAsync("b", 0, [Event(), Event()], T0);
        await store.AppendAsync("a", 1, [Event()], T0);

        var all = await store.ReadAllAsync();
        Assert.Equal([1L, 2L, 3L, 4L], all.Select(e => e.Position));
        Assert.Equal(["a", "b", "b", "a"], all.Select(e => e.StreamId));
    }

    [Fact]
    public async Task AWriterWithAStaleVersion_IsRejected_AndNothingIsWritten()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync("a", 0, [Event()], T0);

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() => store.AppendAsync("a", 0, [Event(), Event()], T0));

        Assert.Equal("a", ex.StreamId);
        Assert.Single(await store.ReadStreamAsync("a"));
        Assert.Single(await store.ReadAllAsync());
    }

    [Fact]
    public async Task AppendingToANewStream_RequiresVersionZero()
    {
        var store = new InMemoryEventStore();

        await Assert.ThrowsAsync<ConcurrencyException>(() => store.AppendAsync("new", 3, [Event()], T0));
    }

    [Fact]
    public async Task Streams_AreIsolated_FromEachOther()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync("a", 0, [Event()], T0);
        await store.AppendAsync("b", 0, [Event(), Event()], T0);

        Assert.Single(await store.ReadStreamAsync("a"));
        Assert.Equal(2, (await store.ReadStreamAsync("b")).Count);
        Assert.Empty(await store.ReadStreamAsync("missing"));
    }

    [Fact]
    public async Task Reading_AfterAVersionOrPosition_ReturnsOnlyTheRest()
    {
        var store = new InMemoryEventStore();
        await store.AppendAsync("a", 0, [Event(), Event(), Event()], T0);

        Assert.Equal([3], (await store.ReadStreamAsync("a", afterVersion: 2)).Select(e => e.Version));
        Assert.Equal([2L, 3L], (await store.ReadAllAsync(afterPosition: 1)).Select(e => e.Position));
    }

    [Fact]
    public async Task StoredEvents_KeepTheirTimestampAndSchemaVersion()
    {
        var store = new InMemoryEventStore();

        await store.AppendAsync("a", 0, [new NewEvent("stock-received", 2, "{}")], T0);

        var stored = Assert.Single(await store.ReadStreamAsync("a"));
        Assert.Equal((T0, 2, "stock-received"), (stored.OccurredAt, stored.SchemaVersion, stored.Type));
    }

    [Fact]
    public void TheStoreOffersOnlyAppendAndRead_ThereIsNoUpdateAndNoDelete()
    {
        var methods = typeof(IEventStore).GetMethods().Select(m => m.Name).ToList();

        Assert.All(methods, name => Assert.True(name.StartsWith("Append") || name.StartsWith("Read"), name));
    }
}

public class EventSerializerTests
{
    private readonly EventSerializer _serializer = EventSerializer.CreateDefault();

    private static StoredEvent Stored(string type, int schemaVersion, string data) =>
        new("stock-X", 1, 1, type, schemaVersion, data, DateTimeOffset.UnixEpoch);

    public static TheoryData<IDomainEvent> AllEvents => new()
    {
        new ItemRegistered("BOOK-1", "Clean Architecture"),
        new StockReceived(10, "Acme"),
        new StockReceived(10, null),
        new StockShipped(3, "ORDER-1"),
        new StockAdjusted(-2, "damaged"),
        new ItemDiscontinued(),
    };

    [Theory]
    [MemberData(nameof(AllEvents))]
    public void EveryEvent_SurvivesAStoreAndReadRoundTrip(IDomainEvent original)
    {
        var written = _serializer.Serialize(original);
        var read = _serializer.Deserialize(Stored(written.Type, written.SchemaVersion, written.Data));

        Assert.Equal(original, read);
    }

    [Fact]
    public void NewEvents_AreWrittenWithTheCurrentSchemaVersion()
    {
        Assert.Equal(2, _serializer.Serialize(new StockReceived(1, null)).SchemaVersion);
        Assert.Equal(1, _serializer.Serialize(new StockShipped(1, "R")).SchemaVersion);
    }

    [Fact]
    public void AnOldVersionOfAnEvent_IsUpcastedWhenRead_WithoutRewritingTheStoredData()
    {
        // History written back when stock-received v1 stored the quantity as "qty" and had no supplier.
        var oldData = """{"qty":7}""";
        var stored = Stored("stock-received", 1, oldData);

        var read = _serializer.Deserialize(stored);

        Assert.Equal(new StockReceived(7, null), read);
        Assert.Equal(oldData, stored.Data);                  // the stored record is untouched
    }

    [Fact]
    public void Deserializing_IgnoresUnknownFields_TolerantReader()
    {
        var read = _serializer.Deserialize(Stored("stock-shipped", 1, """{"quantity":2,"reference":"R","addedLater":true}"""));

        Assert.Equal(new StockShipped(2, "R"), read);
    }

    [Fact]
    public void AnUnknownEventType_IsAnError() =>
        Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(Stored("something-else", 1, "{}")));

    [Fact]
    public void AnEventFromTheFuture_IsAnError_NotSilentlyMisread() =>
        Assert.Throws<InvalidOperationException>(() => _serializer.Deserialize(Stored("stock-shipped", 9, "{}")));

    [Fact]
    public void AMissingUpcaster_IsAnError()
    {
        var serializer = new EventSerializer().Register<StockShipped>("stock-shipped", currentVersion: 3);

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(Stored("stock-shipped", 1, "{}")));

        Assert.Contains("No upcaster", ex.Message);
    }

    [Fact]
    public void Upcasters_CanBeChained_AcrossSeveralVersions()
    {
        var serializer = new EventSerializer()
            .Register<StockAdjusted>("stock-adjusted", currentVersion: 3)
            .Upcast("stock-adjusted", 1, v1 => new JsonObject { ["delta"] = v1["amount"]!.DeepClone(), ["reason"] = null })
            .Upcast("stock-adjusted", 2, v2 => new JsonObject { ["delta"] = v2["delta"]!.DeepClone(), ["reason"] = "legacy" });

        var read = serializer.Deserialize(Stored("stock-adjusted", 1, """{"amount":4}"""));

        Assert.Equal(new StockAdjusted(4, "legacy"), read);
    }

    [Fact]
    public void SerializingAnUnregisteredEvent_IsAnError() =>
        Assert.Throws<InvalidOperationException>(() => new EventSerializer().Serialize(new ItemDiscontinued()));
}
