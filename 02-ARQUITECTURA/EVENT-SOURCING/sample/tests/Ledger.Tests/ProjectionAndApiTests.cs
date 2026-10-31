using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ledger.Domain;
using Ledger.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ledger.Tests;

public class ProjectionTests
{
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryEventStore _store = new();
    private readonly EventSerializer _serializer = EventSerializer.CreateDefault();
    private readonly StockService _service;
    private readonly StockProjection _projection;

    public ProjectionTests()
    {
        _service = new StockService(new StockRepository(_store, new InMemorySnapshotStore(), _serializer, _time));
        _projection = new StockProjection(_store, _serializer);
    }

    [Fact]
    public async Task TheProjection_ShowsTheCurrentStock_BuiltFromAllEvents()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _service.ReceiveAsync("A", 10, "Acme");
        await _service.ShipAsync("A", 3, "R1");
        await _service.RegisterAsync("B", "Beta");

        await _projection.CatchUpAsync();

        Assert.Equal([new StockSummary("A", "Alpha", 7, false), new StockSummary("B", "Beta", 0, false)], _projection.List());
    }

    [Fact]
    public async Task Movements_AreListedNewestFirst_WithTheRunningBalance()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _service.ReceiveAsync("A", 10, "Acme");
        await _service.ShipAsync("A", 3, "R1");
        await _service.AdjustAsync("A", -1, "lost");
        await _projection.CatchUpAsync();

        var movements = _projection.Movements("A")!;

        Assert.Equal([("Adjusted", -1, "lost", 6), ("Shipped", -3, "R1", 7), ("Received", 10, "Acme", 10)],
            movements.Select(m => (m.Kind, m.Delta, m.Detail, m.BalanceAfter)));
    }

    [Fact]
    public async Task ABrokenProjection_IsStaleUntilItCatchesUp()
    {
        await _service.RegisterAsync("A", "Alpha");

        Assert.Null(_projection.Get("A"));                       // the events exist, the read model has not seen them
        await _projection.CatchUpAsync();
        Assert.NotNull(_projection.Get("A"));
    }

    [Fact]
    public async Task CatchingUpTwice_DoesNotApplyAnythingTwice()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _service.ReceiveAsync("A", 5, null);
        await _projection.CatchUpAsync();

        var second = await _projection.CatchUpAsync();

        Assert.Equal(0, second);
        Assert.Equal(5, _projection.Get("A")!.OnHand);
    }

    [Fact]
    public async Task TheProjection_ResumesFromItsCheckpoint()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _projection.CatchUpAsync();
        await _service.ReceiveAsync("A", 4, null);

        Assert.Equal(1, await _projection.CatchUpAsync());
        Assert.Equal(4, _projection.Get("A")!.OnHand);
        Assert.Equal(2, _projection.Checkpoint);
    }

    [Fact]
    public async Task Rebuilding_FromTheEvents_ReproducesTheSameReadModel()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _service.ReceiveAsync("A", 10, "Acme");
        await _service.RegisterAsync("B", "Beta");
        await _service.ReceiveAsync("B", 2, null);
        await _service.ShipAsync("A", 3, "R1");
        await _service.AdjustAsync("B", -1, "lost");
        await _projection.CatchUpAsync();
        var before = _projection.List();
        var movementsBefore = _projection.Movements("A");

        var replayed = await _projection.RebuildAsync();

        Assert.Equal(6, replayed);
        Assert.Equal(before, _projection.List());
        Assert.Equal(movementsBefore, _projection.Movements("A"));
    }

    [Fact]
    public async Task AnOldVersionOfAnEvent_IsUpcastedBeforeTheProjectionSeesIt()
    {
        await _service.RegisterAsync("A", "Alpha");
        // A legacy event, written long ago by an older version of the application (schema v1, field "qty").
        await _store.AppendAsync("stock-A", 1, [new NewEvent("stock-received", 1, """{"qty":7}""")], _time.GetUtcNow());

        await _projection.CatchUpAsync();

        Assert.Equal(7, _projection.Get("A")!.OnHand);
    }

    [Fact]
    public async Task ADiscontinuedItem_IsFlagged()
    {
        await _service.RegisterAsync("A", "Alpha");
        await _service.DiscontinueAsync("A");

        await _projection.CatchUpAsync();

        Assert.True(_projection.Get("A")!.Discontinued);
    }

    [Fact]
    public void AnUnknownItem_HasNoSummaryAndNoMovements()
    {
        Assert.Null(_projection.Get("NOPE"));
        Assert.Null(_projection.Movements("NOPE"));
    }

    [Fact]
    public async Task ASecondProjection_CanBeBuiltFromTheSameEvents_WithADifferentShape()
    {
        // The point of keeping events: a NEW read model (here, "total shipped") needs no change to the write side.
        await _service.RegisterAsync("A", "Alpha");
        await _service.ReceiveAsync("A", 10, null);
        await _service.ShipAsync("A", 3, "R1");
        await _service.ShipAsync("A", 2, "R2");

        var totalShipped = (await _store.ReadAllAsync())
            .Select(_serializer.Deserialize).OfType<StockShipped>().Sum(s => s.Quantity);

        Assert.Equal(5, totalShipped);
    }
}

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Fresh() => factory.WithWebHostBuilder(_ => { }).CreateClient();

    private static async Task Register(HttpClient c, string sku = "BOOK-1") =>
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/items", new { sku, name = "Clean Architecture" })).StatusCode);

    [Fact]
    public async Task ARegisteredItem_StartsWithNoStock()
    {
        var client = Fresh();

        await Register(client);

        var item = await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1");
        Assert.Equal((0, false), (item.GetProperty("onHand").GetInt32(), item.GetProperty("discontinued").GetBoolean()));
    }

    [Fact]
    public async Task ReceivingAndShipping_ChangeTheStock_AndLeaveMovements()
    {
        var client = Fresh();
        await Register(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 10, supplier = "Acme" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/items/BOOK-1/ship", new { quantity = 4, reference = "ORD-1" })).StatusCode);

        Assert.Equal(6, (await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1")).GetProperty("onHand").GetInt32());
        var movements = await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1/movements");
        Assert.Equal(["Shipped", "Received"], movements.EnumerateArray().Select(m => m.GetProperty("kind").GetString()!));
    }

    [Fact]
    public async Task TheEventsEndpoint_ShowsTheStoredFacts_WithTheirSchemaVersion()
    {
        var client = Fresh();
        await Register(client);
        await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 3, supplier = "Acme" });

        var events = await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1/events");

        var last = events.EnumerateArray().Last();
        Assert.Equal(("stock-received", 2, 2), (last.GetProperty("type").GetString(), last.GetProperty("schemaVersion").GetInt32(), last.GetProperty("version").GetInt32()));
        Assert.Contains("Acme", last.GetProperty("data").GetString());
    }

    [Fact]
    public async Task ShippingMoreThanOnHand_Returns422_AndAddsNoEvent()
    {
        var client = Fresh();
        await Register(client);
        await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 2, supplier = (string?)null });

        var response = await client.PostAsJsonAsync("/items/BOOK-1/ship", new { quantity = 3, reference = "ORD-1" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("only 2 on hand", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1/events")).GetArrayLength());
    }

    [Fact]
    public async Task ACorrection_IsAdjustment_NotAnEdit()
    {
        var client = Fresh();
        await Register(client);
        await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 10, supplier = (string?)null });

        await client.PostAsJsonAsync("/items/BOOK-1/adjust", new { delta = -2, reason = "damaged" });

        Assert.Equal(8, (await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1")).GetProperty("onHand").GetInt32());
        Assert.Equal(3, (await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1/events")).GetArrayLength());   // nothing was rewritten
    }

    [Fact]
    public async Task AnAdjustment_WithoutReason_Returns422() =>
        await Assert422(async c =>
        {
            await Register(c);
            await c.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 5, supplier = (string?)null });
            return await c.PostAsJsonAsync("/items/BOOK-1/adjust", new { delta = 1, reason = "" });
        });

    [Fact]
    public async Task RegisteringTwice_Returns422() =>
        await Assert422(async c =>
        {
            await Register(c);
            return await c.PostAsJsonAsync("/items", new { sku = "book-1", name = "again" });
        });

    [Fact]
    public async Task DiscontinuingAnItemWithStock_Returns422() =>
        await Assert422(async c =>
        {
            await Register(c);
            await c.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 1, supplier = (string?)null });
            return await c.PostAsync("/items/BOOK-1/discontinue", null);
        });

    private async Task Assert422(Func<HttpClient, Task<HttpResponseMessage>> act) =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await act(Fresh())).StatusCode);

    [Fact]
    public async Task UnknownItems_Return404()
    {
        var client = Fresh();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/items/NOPE")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/items/NOPE/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/items/NOPE/movements")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/items/NOPE/receive", new { quantity = 1 })).StatusCode);
    }

    [Fact]
    public async Task AsOf_ReplaysTheHistoryUpToThatMoment()
    {
        var client = Fresh();
        var beforeEverything = DateTimeOffset.UtcNow.AddMinutes(-5);
        await Register(client);
        await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 10, supplier = (string?)null });
        var later = DateTimeOffset.UtcNow.AddMinutes(5);

        var past = await client.GetAsync($"/items/BOOK-1/as-of?at={Uri.EscapeDataString(beforeEverything.ToString("O"))}");
        var future = await client.GetFromJsonAsync<JsonElement>($"/items/BOOK-1/as-of?at={Uri.EscapeDataString(later.ToString("O"))}");

        Assert.Equal(HttpStatusCode.NotFound, past.StatusCode);                       // it did not exist yet
        Assert.Equal(10, future.GetProperty("onHand").GetInt32());
    }

    [Fact]
    public async Task RebuildingTheProjection_ReplaysEveryEvent()
    {
        var client = Fresh();
        await Register(client);
        await client.PostAsJsonAsync("/items/BOOK-1/receive", new { quantity = 10, supplier = (string?)null });
        await client.GetAsync("/items");

        var rebuilt = await (await client.PostAsync("/admin/rebuild-projection", null)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, rebuilt.GetProperty("replayed").GetInt32());
        Assert.Equal(10, (await client.GetFromJsonAsync<JsonElement>("/items/BOOK-1")).GetProperty("onHand").GetInt32());
    }
}
