using Catalog.Service;
using Gateway;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orders.Service;

namespace Platform.Tests;

/// <summary>Wraps the network between two services so a test can make it slow, flaky or dead.</summary>
internal sealed class ScriptedLink : DelegatingHandler
{
    private int _calls;

    public ScriptedLink(HttpMessageHandler inner) : base(inner) { }

    public List<string> Calls { get; } = [];

    /// <summary>(request, sendToRealService, callNumber) → response. Default: pass through.</summary>
    public Func<HttpRequestMessage, Func<Task<HttpResponseMessage>>, int, Task<HttpResponseMessage>> Behavior { get; set; } =
        (_, next, _) => next();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
        var callNumber = Interlocked.Increment(ref _calls);
        return Behavior(request, () => base.SendAsync(request, cancellationToken), callNumber);
    }
}

/// <summary>The three services started in memory and wired like they would be over the network.</summary>
internal sealed class Platform : IDisposable
{
    private readonly WebApplicationFactory<CatalogServiceEntry> _catalog;
    private readonly WebApplicationFactory<OrdersServiceEntry> _orders;
    private readonly WebApplicationFactory<GatewayEntry> _gateway;

    public Platform()
    {
        _catalog = new WebApplicationFactory<CatalogServiceEntry>();
        var catalogDirect = _catalog.Server.CreateHandler();

        // Orders → Catalog goes through a link we control.
        OrdersToCatalog = new ScriptedLink(catalogDirect);
        _orders = new WebApplicationFactory<OrdersServiceEntry>().WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(() => OrdersToCatalog)));
        var ordersDirect = _orders.Server.CreateHandler();

        // Gateway → services, also through links.
        GatewayToCatalog = new ScriptedLink(catalogDirect);
        GatewayToOrders = new ScriptedLink(ordersDirect);
        _gateway = new WebApplicationFactory<GatewayEntry>().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(() => GatewayToCatalog);
            s.AddHttpClient("orders").ConfigurePrimaryHttpMessageHandler(() => GatewayToOrders);
        }));
    }

    public ScriptedLink OrdersToCatalog { get; }
    public ScriptedLink GatewayToCatalog { get; }
    public ScriptedLink GatewayToOrders { get; }

    /// <summary>Talks to a service directly, as an operator would (bypassing every scripted link).</summary>
    public HttpClient Catalog => _catalog.CreateClient();
    public HttpClient Orders => _orders.CreateClient();
    public HttpClient Gateway => _gateway.CreateClient();

    public void Dispose()
    {
        _gateway.Dispose();
        _orders.Dispose();
        _catalog.Dispose();
    }
}
