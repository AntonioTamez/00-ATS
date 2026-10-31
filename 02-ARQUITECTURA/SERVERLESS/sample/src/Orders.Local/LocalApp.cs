using Faas.Runtime;
using Orders.Functions;

namespace Orders.Local;

/// <summary>A function app running on this machine: the platform, its queues and the store behind the functions.</summary>
public sealed class LocalApp
{
    private LocalApp(FunctionHost host, InMemoryMessageBus bus, InMemoryOrderStore store)
    {
        Host = host;
        Bus = bus;
        Store = store;
    }

    public FunctionHost Host { get; }

    public InMemoryMessageBus Bus { get; }

    public InMemoryOrderStore Store { get; }

    /// <summary>Which functions exist, how each one is triggered and with which limits. Nothing else lives here.</summary>
    public static LocalApp Create(IReadOnlyDictionary<string, int> stock, HostOptions? options = null, int reserveConcurrency = 1)
    {
        var bus = new InMemoryMessageBus();
        var store = new InMemoryOrderStore(stock);
        var host = new FunctionHost(bus, options)
            .AddHttp("PlaceOrder", "POST", "/orders", () => new PlaceOrderFunction(store))
            .AddQueue("ReserveStock", "orders", () => new ReserveStockFunction(store),
                new FunctionOptions { Timeout = TimeSpan.FromSeconds(2), MaxDeliveryCount = 3, MaxConcurrency = reserveConcurrency })
            .AddTimer("DailySummary", () => new DailySummaryFunction(store));

        return new LocalApp(host, bus, store);
    }
}
