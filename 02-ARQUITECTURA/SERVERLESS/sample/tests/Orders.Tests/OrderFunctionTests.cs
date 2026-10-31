using System.Text.Json;
using Faas.Runtime;
using Orders.Functions;
using Orders.Local;
using static Orders.Tests.Support;

namespace Orders.Tests;

/// <summary>The three business functions, called directly like any method: a function is just code with inputs and outputs.</summary>
public class OrderFunctionTests
{
    private static InMemoryOrderStore Store(int abc = 5) =>
        new(new Dictionary<string, int> { ["ABC-1"] = abc, ["XYZ-2"] = 1 });

    private static HttpRequest Post(string body, string? key = "k1") =>
        new("POST", "/orders", body, key is null ? null : new Dictionary<string, string> { ["Idempotency-Key"] = key });

    private const string ValidBody = """{"customer":"ana","sku":"abc-1","quantity":2}""";

    private static QueueMessage Message(string orderId, string id = "m1") =>
        new(id, "orders", JsonSerializer.Serialize(new { orderId }), 1);

    // ---------- PlaceOrder ----------

    [Fact]
    public async Task PlaceOrder_StoresTheOrder_AnswersAcceptedAndHandsTheWorkToTheQueue()
    {
        var store = Store();
        var response = await new PlaceOrderFunction(store).HandleAsync(Post(ValidBody), Context());

        Assert.Equal(202, response.Status);
        Assert.Equal("""{"orderId":"k1"}""", response.Body);
        var message = Assert.Single(response.Outputs!.Messages);
        Assert.Equal(("orders", """{"orderId":"k1"}""", "k1"), (message.Queue, message.Body, message.MessageId));
        var order = Assert.Single(await store.ListAsync());
        Assert.Equal(new Order("k1", "ana", "ABC-1", 2), order);
    }

    [Fact]
    public async Task PlaceOrder_TheSameKeyTwice_CreatesOneOrderAndSendsOneMessage()
    {
        var store = Store();
        var function = new PlaceOrderFunction(store);

        var first = await function.HandleAsync(Post(ValidBody), Context());
        var second = await function.HandleAsync(Post(ValidBody), Context());

        Assert.Equal(202, first.Status);
        Assert.Equal(200, second.Status);
        Assert.Contains("already received", second.Body);
        Assert.Null(second.Outputs);
        Assert.Single(await store.ListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlaceOrder_WithoutAnIdempotencyKey_IsRefused(string? key)
    {
        var store = Store();
        var response = await new PlaceOrderFunction(store).HandleAsync(Post(ValidBody, key), Context());

        Assert.Equal(400, response.Status);
        Assert.Contains("Idempotency-Key", response.Body);
        Assert.Empty(await store.ListAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"customer":"ana","sku":"abc-1","quantity":"two"}""")]
    public async Task PlaceOrder_InvalidJson_IsA400(string body)
    {
        var response = await new PlaceOrderFunction(Store()).HandleAsync(Post(body), Context());

        Assert.Equal((400, """{"error":"invalid JSON"}"""), (response.Status, response.Body));
    }

    [Theory]
    [InlineData("""{"sku":"abc-1","quantity":1}""")]
    [InlineData("""{"customer":"ana","quantity":1}""")]
    [InlineData("""{"customer":"ana","sku":"abc-1"}""")]
    [InlineData("""{"customer":"ana","sku":"abc-1","quantity":0}""")]
    [InlineData("""{"customer":"ana","sku":"abc-1","quantity":-3}""")]
    [InlineData("null")]
    public async Task PlaceOrder_MissingOrInvalidFields_AreA422_AndStoreNothing(string body)
    {
        var store = Store();
        var response = await new PlaceOrderFunction(store).HandleAsync(Post(body), Context());

        Assert.Equal(422, response.Status);
        Assert.Null(response.Outputs);
        Assert.Empty(await store.ListAsync());
    }

    // ---------- ReserveStock ----------

    private static async Task<InMemoryOrderStore> StoreWithOrder(int quantity, string sku = "ABC-1", int abc = 5)
    {
        var store = Store(abc);
        await store.TryAddAsync(new Order("k1", "ana", sku, quantity));
        return store;
    }

    [Fact]
    public async Task ReserveStock_ReservesTheStock_AndAnnouncesIt()
    {
        var store = await StoreWithOrder(2);

        var outputs = await new ReserveStockFunction(store).HandleAsync(Message("k1"), Context());

        var announced = Assert.Single(outputs.Messages);
        Assert.Equal(("order-events", """{"type":"OrderReserved","orderId":"k1"}""", "m1:OrderReserved"),
            (announced.Queue, announced.Body, announced.MessageId));
        Assert.Equal(3, store.StockOf("ABC-1"));
        Assert.Equal(OrderStatus.Reserved, (await store.FindAsync("k1"))!.Status);
    }

    [Fact]
    public async Task ReserveStock_NotEnoughStock_IsABusinessAnswerNotAFailure()
    {
        var store = await StoreWithOrder(3, "XYZ-2");

        var outputs = await new ReserveStockFunction(store).HandleAsync(Message("k1"), Context());

        Assert.Contains("OrderRejected", Assert.Single(outputs.Messages).Body);
        Assert.Equal(1, store.StockOf("XYZ-2"));
        Assert.Equal(OrderStatus.OutOfStock, (await store.FindAsync("k1"))!.Status);
    }

    [Fact]
    public async Task ReserveStock_TheSameMessageTwice_ReservesOnce()
    {
        var store = await StoreWithOrder(2);
        var function = new ReserveStockFunction(store);

        var first = await function.HandleAsync(Message("k1"), Context());
        var second = await function.HandleAsync(Message("k1"), Context(attempt: 2));

        Assert.Single(first.Messages);
        Assert.Empty(second.Messages);
        Assert.Equal(3, store.StockOf("ABC-1"));
    }

    [Fact]
    public async Task ReserveStock_TheSameOrderUnderAnotherMessageId_StillReservesOnce()
    {
        var store = await StoreWithOrder(2);
        var function = new ReserveStockFunction(store);

        await function.HandleAsync(Message("k1", "m1"), Context());
        var again = await function.HandleAsync(Message("k1", "m2"), Context());

        Assert.Empty(again.Messages);
        Assert.Equal(3, store.StockOf("ABC-1"));
    }

    [Fact]
    public async Task ReserveStock_AnOrderThatIsNotThereYet_Throws_SoThePlatformRetriesIt()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReserveStockFunction(Store()).HandleAsync(Message("ghost"), Context()));

        Assert.Equal("order 'ghost' not found", failure.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"orderId":5}""")]
    [InlineData("""{"orderId":""}""")]
    [InlineData("[]")]
    public async Task ReserveStock_AMalformedMessage_Throws(string body)
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReserveStockFunction(Store()).HandleAsync(new QueueMessage("m1", "orders", body, 1), Context()));

        Assert.Equal("malformed message body", failure.Message);
    }

    // ---------- DailySummary ----------

    [Fact]
    public async Task DailySummary_CountsOrdersByStatus_UsingTheContextClock()
    {
        var store = Store();
        await store.TryAddAsync(new Order("a", "x", "ABC-1", 1));
        await store.TryAddAsync(new Order("b", "x", "XYZ-2", 5));
        await store.TryAddAsync(new Order("c", "x", "ABC-1", 1));
        await store.ReserveOnceAsync("m-a", "a");
        await store.ReserveOnceAsync("m-b", "b");

        var outputs = await new DailySummaryFunction(store).HandleAsync(new TimerInfo(Now), Context());

        var report = Assert.Single(outputs.Messages);
        Assert.Equal("reports", report.Queue);
        Assert.Equal("""{"date":"2026-03-14","received":1,"reserved":1,"outOfStock":1}""", report.Body);
    }

    [Fact]
    public async Task DailySummary_RunningItTwice_GivesTheSameAnswer()
    {
        var store = await StoreWithOrder(1);
        var function = new DailySummaryFunction(store);

        var first = await function.HandleAsync(new TimerInfo(Now), Context());
        var second = await function.HandleAsync(new TimerInfo(Now), Context());

        Assert.Equal(first.Messages[0].Body, second.Messages[0].Body);
    }

    // ---------- the store (every operation must be atomic: the platform runs invocations in parallel) ----------

    [Fact]
    public async Task Store_AHundredParallelCopiesOfOneMessage_ReserveOnce()
    {
        var store = await StoreWithOrder(2);

        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => store.ReserveOnceAsync("m1", "k1"))));

        Assert.Equal(1, results.Count(r => r == ReserveResult.Reserved));
        Assert.Equal(99, results.Count(r => r == ReserveResult.AlreadyProcessed));
        Assert.Equal(3, store.StockOf("ABC-1"));
    }

    [Fact]
    public async Task Store_ParallelOrdersNeverOversell()
    {
        var store = Store(abc: 20);
        for (var i = 0; i < 50; i++)
            await store.TryAddAsync(new Order($"o{i}", "x", "ABC-1", 1));

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => store.ReserveOnceAsync($"m{i}", $"o{i}"))));

        Assert.Equal(20, results.Count(r => r == ReserveResult.Reserved));
        Assert.Equal(30, results.Count(r => r == ReserveResult.OutOfStock));
        Assert.Equal(0, store.StockOf("ABC-1"));
    }

    [Fact]
    public async Task Store_TryAddRefusesADuplicateId()
    {
        var store = Store();

        Assert.True(await store.TryAddAsync(new Order("k1", "ana", "ABC-1", 1)));
        Assert.False(await store.TryAddAsync(new Order("k1", "bob", "XYZ-2", 9)));
        Assert.Equal("ana", (await store.FindAsync("k1"))!.Customer);
    }
}
