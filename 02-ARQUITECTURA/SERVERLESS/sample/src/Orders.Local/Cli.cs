using System.Text.Json;
using Faas.Runtime;

namespace Orders.Local;

/// <summary>
/// <c>Orders.Local demo [--recycle]</c>: runs a fixed scenario through the local platform and prints what happened.
/// <c>--recycle</c> makes the platform create a new function instance for every invocation. Exit codes: 0 ok, 2 bad usage.
/// </summary>
public static class Cli
{
    private const string Usage = "usage: Orders.Local demo [--recycle]";

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length is < 1 or > 2 || args[0] != "demo" || (args.Length == 2 && args[1] != "--recycle"))
        {
            await stderr.WriteLineAsync(Usage);
            return 2;
        }

        var app = LocalApp.Create(
            new Dictionary<string, int> { ["ABC-1"] = 5, ["XYZ-2"] = 1 },
            new HostOptions { RecycleInstances = args.Length == 2 });

        async Task Post(string key, string body)
        {
            var headers = key.Length == 0 ? null : new Dictionary<string, string> { ["Idempotency-Key"] = key };
            var response = await app.Host.InvokeHttpAsync(new HttpRequest("POST", "/orders", body, headers));
            await stdout.WriteLineAsync($"POST /orders [{(key.Length == 0 ? "no key" : key)}] -> {response.Status} {response.Body}");
        }

        await Post("k1", """{"customer":"ana","sku":"abc-1","quantity":2}""");
        await Post("k1", """{"customer":"ana","sku":"abc-1","quantity":2}""");
        await Post("k2", """{"customer":"bob","sku":"xyz-2","quantity":3}""");
        await Post("k3", """{"customer":"eve","sku":"abc-1","quantity":0}""");
        await Post("", """{"customer":"eve","sku":"abc-1","quantity":1}""");

        // The platform delivers at least once: the same message can show up again.
        app.Bus.Send("orders", """{"orderId":"k1"}""", "k1");
        // A message that no retry can fix.
        app.Bus.Send("orders", "not json", "poison-1");

        var invocations = await app.Host.DrainQueuesAsync();
        await stdout.WriteLineAsync($"Drained the queues in {invocations} invocations");

        foreach (var letter in app.Bus.DeadLetters("orders"))
            await stdout.WriteLineAsync($"Dead letter [{letter.Message.Id}] after {letter.Message.DeliveryCount} deliveries: {letter.Reason}");
        foreach (var message in app.Bus.Peek("order-events"))
            await stdout.WriteLineAsync($"Event {message.Body}");
        await stdout.WriteLineAsync($"Stock: ABC-1={app.Store.StockOf("ABC-1")} XYZ-2={app.Store.StockOf("XYZ-2")}");

        await app.Host.FireTimerAsync("DailySummary");
        var report = app.Bus.Peek("reports").Single();
        using var summary = JsonDocument.Parse(report.Body);
        var root = summary.RootElement;
        await stdout.WriteLineAsync(
            $"Summary: received={root.GetProperty("received")} reserved={root.GetProperty("reserved")} outOfStock={root.GetProperty("outOfStock")}");

        await stdout.WriteLineAsync("Invocations:");
        foreach (var record in app.Host.Invocations)
            await stdout.WriteLineAsync(
                $"  {record.Function,-12} {record.Trigger,-5} attempt {record.Attempt} {record.Outcome}{(record.Error is null ? "" : " (" + record.Error + ")")}");
        await stdout.WriteLineAsync(
            $"Instances created: PlaceOrder={app.Host.ColdStarts("PlaceOrder")} ReserveStock={app.Host.ColdStarts("ReserveStock")} DailySummary={app.Host.ColdStarts("DailySummary")}");

        return 0;
    }
}
