using Faas.Runtime;
using Orders.Functions;
using Orders.Local;
using static Orders.Tests.Support;

namespace Orders.Tests;

/// <summary>The functions running on the local platform, as a cloud would run them.</summary>
public class EndToEndTests
{
    private static LocalApp NewApp(bool recycle = false, int abc = 5) => LocalApp.Create(
        new Dictionary<string, int> { ["ABC-1"] = abc, ["XYZ-2"] = 1 },
        new HostOptions { RecycleInstances = recycle, Clock = new FixedClock(Now) });

    private static Task<HttpResponse> Place(LocalApp app, string key, string sku, int quantity) =>
        app.Host.InvokeHttpAsync(new HttpRequest("POST", "/orders",
            $$"""{"customer":"ana","sku":"{{sku}}","quantity":{{quantity}}}""",
            new Dictionary<string, string> { ["Idempotency-Key"] = key }));

    /// <summary>Runs one scenario and returns everything observable about it, as text.</summary>
    private static async Task<string> Scenario(bool recycle)
    {
        var app = NewApp(recycle);
        var lines = new List<string>();

        foreach (var (key, sku, quantity) in new[] { ("k1", "abc-1", 2), ("k1", "abc-1", 2), ("k2", "xyz-2", 3), ("k3", "abc-1", 1) })
        {
            var response = await Place(app, key, sku, quantity);
            lines.Add($"POST {key} {response.Status} {response.Body}");
        }

        app.Bus.Send("orders", """{"orderId":"k1"}""", "k1"); // the platform delivers it a second time
        app.Bus.Send("orders", "not json", "poison-1");
        lines.Add($"drained {await app.Host.DrainQueuesAsync()}");
        lines.AddRange(app.Bus.DeadLetters("orders").Select(d => $"dead {d.Message.Id} {d.Message.DeliveryCount} {d.Reason}"));
        lines.AddRange(app.Bus.Peek("order-events").Select(m => $"event {m.Id} {m.Body}"));
        lines.Add($"stock {app.Store.StockOf("ABC-1")} {app.Store.StockOf("XYZ-2")}");
        await app.Host.FireTimerAsync("DailySummary");
        lines.AddRange(app.Bus.Peek("reports").Select(m => $"report {m.Body}"));
        lines.AddRange(app.Host.Invocations.Select(r => $"{r.Function} {r.Trigger} {r.Attempt} {r.Outcome} {r.Error}"));
        return string.Join('\n', lines);
    }

    [Fact]
    public async Task APostAnswers202BeforeAnyStockIsTouched_TheWorkHappensLaterOnTheQueue()
    {
        var app = NewApp();

        var response = await Place(app, "k1", "abc-1", 2);

        Assert.Equal(202, response.Status);
        Assert.Equal(5, app.Store.StockOf("ABC-1"));
        Assert.Equal(1, app.Bus.Pending("orders"));

        await app.Host.DrainQueuesAsync();

        Assert.Equal(3, app.Store.StockOf("ABC-1"));
        Assert.Equal(0, app.Bus.Pending("orders"));
    }

    [Fact]
    public async Task APlatformRedelivery_DoesNotReserveTwice()
    {
        var app = NewApp();
        await Place(app, "k1", "abc-1", 2);
        app.Bus.Send("orders", """{"orderId":"k1"}""", "k1");
        app.Bus.Send("orders", """{"orderId":"k1"}""", "k1");

        await app.Host.DrainQueuesAsync();

        Assert.Equal(3, app.Store.StockOf("ABC-1"));
        Assert.Equal(["OrderReserved"], app.Bus.Peek("order-events").Select(m => m.Body.Contains("OrderReserved") ? "OrderReserved" : "?"));
        Assert.Equal(3, app.Host.Invocations.Count(r => r.Function == "ReserveStock"));
    }

    [Fact]
    public async Task APoisonMessage_IsDeadLetteredAfterThreeDeliveries_AndTheGoodOnesAreUnharmed()
    {
        var app = NewApp();
        app.Bus.Send("orders", "not json", "poison-1");
        await Place(app, "k1", "abc-1", 2);

        await app.Host.DrainQueuesAsync();

        var dead = Assert.Single(app.Bus.DeadLetters("orders"));
        Assert.Equal(("poison-1", 3, "malformed message body"), (dead.Message.Id, dead.Message.DeliveryCount, dead.Reason));
        Assert.Equal(3, app.Store.StockOf("ABC-1"));
        Assert.Equal(0, app.Bus.Pending("orders"));
    }

    [Fact]
    public async Task ABurstOfOrders_SellsExactlyTheStock_EvenWithEightInvocationsAtOnce()
    {
        var app = LocalApp.Create(
            new Dictionary<string, int> { ["ABC-1"] = 20 },
            new HostOptions { Clock = new FixedClock(Now) },
            reserveConcurrency: 8);
        for (var i = 0; i < 50; i++)
            await Place(app, $"k{i}", "abc-1", 1);

        await app.Host.DrainQueuesAsync();

        var events = app.Bus.Peek("order-events").Select(m => m.Body).ToList();
        Assert.Equal(20, events.Count(e => e.Contains("OrderReserved")));
        Assert.Equal(30, events.Count(e => e.Contains("OrderRejected")));
        Assert.Equal(0, app.Store.StockOf("ABC-1"));
    }

    [Fact]
    public async Task TheResultIsTheSame_WhetherThePlatformKeepsInstancesWarmOrCreatesANewOneEveryTime()
    {
        var warm = await Scenario(recycle: false);
        var recycled = await Scenario(recycle: true);

        Assert.Equal(warm, recycled);
        Assert.Contains("dead poison-1 3 malformed message body", warm);
        Assert.Contains("stock 2 1", warm);
        Assert.Contains("report {\"date\":\"2026-03-14\",\"received\":0,\"reserved\":2,\"outOfStock\":1}", warm);
    }

    [Fact]
    public async Task OnlyTheNumberOfCreatedInstancesDiffersBetweenWarmAndRecycled()
    {
        var warm = NewApp(recycle: false);
        var cold = NewApp(recycle: true);
        foreach (var app in new[] { warm, cold })
        {
            await Place(app, "k1", "abc-1", 1);
            await Place(app, "k2", "abc-1", 1);
            await app.Host.DrainQueuesAsync();
        }

        Assert.Equal((1, 1), (warm.Host.ColdStarts("PlaceOrder"), warm.Host.ColdStarts("ReserveStock")));
        Assert.Equal((2, 2), (cold.Host.ColdStarts("PlaceOrder"), cold.Host.ColdStarts("ReserveStock")));
    }

    [Fact]
    public async Task KnownLimit_ACrashBetweenTheEffectAndTheAnnouncement_LosesTheEvent()
    {
        // The stock is reserved exactly once (idempotency works), but the "OrderReserved" event is gone:
        // the effect and the message are two separate writes. The usual fix is an outbox, which this sample does not have.
        var bus = new InMemoryMessageBus();
        var inner = new InMemoryOrderStore(new Dictionary<string, int> { ["ABC-1"] = 5 });
        await inner.TryAddAsync(new Order("k1", "ana", "ABC-1", 2));
        var host = new FunctionHost(bus).AddQueue("ReserveStock", "orders", () => new ReserveStockFunction(new CrashAfterReserve(inner)));
        bus.Send("orders", """{"orderId":"k1"}""", "k1");

        await host.DrainQueuesAsync();

        Assert.Equal([Outcome.Failed, Outcome.Succeeded], host.Invocations.Select(r => r.Outcome));
        Assert.Equal(3, inner.StockOf("ABC-1"));
        Assert.Equal(OrderStatus.Reserved, (await inner.FindAsync("k1"))!.Status);
        Assert.Empty(bus.Peek("order-events"));
    }

    /// <summary>A store that does the work and then "crashes" the first time, before the function can announce it.</summary>
    private sealed class CrashAfterReserve(IOrderStore inner) : IOrderStore
    {
        private int _crashes;

        public Task<bool> TryAddAsync(Order order) => inner.TryAddAsync(order);

        public Task<Order?> FindAsync(string orderId) => inner.FindAsync(orderId);

        public Task<IReadOnlyList<Order>> ListAsync() => inner.ListAsync();

        public async Task<ReserveResult> ReserveOnceAsync(string messageId, string orderId)
        {
            var result = await inner.ReserveOnceAsync(messageId, orderId);
            if (Interlocked.Increment(ref _crashes) == 1)
                throw new InvalidOperationException("crash after reserving");
            return result;
        }
    }

    // ---------- the command line ----------

    private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await Cli.RunAsync(args, stdout, stderr);
        return (code, stdout.ToString().ReplaceLineEndings("\n"), stderr.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Cli_Demo_ShowsTheWholeStory()
    {
        var (code, output, error) = await Run("demo");

        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("POST /orders [k1] -> 202 {\"orderId\":\"k1\"}\n", output);
        Assert.Contains("POST /orders [k1] -> 200 {\"orderId\":\"k1\",\"status\":\"already received\"}\n", output);
        Assert.Contains("POST /orders [k3] -> 422 ", output);
        Assert.Contains("POST /orders [no key] -> 400 ", output);
        Assert.Contains("Drained the queues in 6 invocations\n", output);
        Assert.Contains("Dead letter [poison-1] after 3 deliveries: malformed message body\n", output);
        Assert.Contains("Stock: ABC-1=3 XYZ-2=1\n", output);
        Assert.Contains("Summary: received=0 reserved=1 outOfStock=1\n", output);
        Assert.Contains("Instances created: PlaceOrder=1 ReserveStock=1 DailySummary=1\n", output);
    }

    [Fact]
    public async Task Cli_DemoRecycle_ChangesOnlyTheInstancesCreated()
    {
        var warm = (await Run("demo")).Out;
        var recycled = (await Run("demo", "--recycle")).Out;

        Assert.Contains("Instances created: PlaceOrder=5 ReserveStock=6 DailySummary=1\n", recycled);
        Assert.Equal(
            warm.Split('\n').Where(line => !line.StartsWith("Instances created")),
            recycled.Split('\n').Where(line => !line.StartsWith("Instances created")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("demo --warm")]
    [InlineData("demo --recycle extra")]
    public async Task Cli_BadUsage_ExitsWithTwo(string arguments)
    {
        var (code, output, error) = await Run(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Equal("usage: Orders.Local demo [--recycle]\n", error);
    }
}
