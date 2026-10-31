using System.Net;
using System.Net.Http.Json;
using Catalog.Service;

namespace Platform.Tests;

/// <summary>The single entry point: it routes, it does not decide anything.</summary>
public class GatewayTests : IDisposable
{
    private readonly Platform _platform = new();

    public void Dispose() => _platform.Dispose();

    private static object NewOrder(string email, string sku, int quantity) =>
        new { customerEmail = email, lines = new[] { new { sku, quantity } } };

    [Fact]
    public async Task Health_IsOk() =>
        Assert.Equal(HttpStatusCode.OK, (await _platform.Gateway.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task CatalogRoute_StripsThePrefix_AndReturnsTheServiceAnswer()
    {
        var products = await _platform.Gateway.GetFromJsonAsync<List<ProductDto>>("/api/catalog/products");

        Assert.Equal(3, products!.Count);
        Assert.Equal(["GET /products"], _platform.GatewayToCatalog.Calls);
    }

    [Fact]
    public async Task Routes_KeepThePathBelowThePrefix()
    {
        var product = await _platform.Gateway.GetFromJsonAsync<ProductDto>("/api/catalog/products/BOOK-1");

        Assert.Equal("BOOK-1", product!.Sku);
        Assert.Equal(["GET /products/BOOK-1"], _platform.GatewayToCatalog.Calls);
    }

    [Fact]
    public async Task DownstreamStatusCodes_PassThroughUnchanged() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _platform.Gateway.GetAsync("/api/catalog/products/NOPE")).StatusCode);

    [Fact]
    public async Task OrdersRoute_ForwardsTheBody_AndTheLocationHeader()
    {
        var response = await _platform.Gateway.PostAsJsonAsync("/api/orders/orders", NewOrder("ana@x.com", "PEN-1", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/orders/", response.Headers.Location!.ToString());
        Assert.Contains("\"total\":3", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheIdempotencyKeyHeader_ReachesTheService()
    {
        async Task<HttpResponseMessage> PostAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/orders") { Content = JsonContent.Create(NewOrder("ana@x.com", "MUG-1", 1)) };
            request.Headers.Add("Idempotency-Key", "gw-1");
            return await _platform.Gateway.SendAsync(request);
        }

        var first = await PostAsync();
        var second = await PostAsync();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task UnknownService_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _platform.Gateway.GetAsync("/api/billing/invoices")).StatusCode);

    [Fact]
    public async Task WhenAServiceIsUnreachable_TheGatewayAnswers502()
    {
        _platform.GatewayToCatalog.Behavior = (_, _, _) => throw new HttpRequestException("connection refused");

        Assert.Equal(HttpStatusCode.BadGateway, (await _platform.Gateway.GetAsync("/api/catalog/products")).StatusCode);
    }

    [Fact]
    public async Task WhenAServiceTimesOut_TheGatewayAnswers504()
    {
        _platform.GatewayToCatalog.Behavior = (_, _, _) => throw new TaskCanceledException("timeout");

        Assert.Equal(HttpStatusCode.GatewayTimeout, (await _platform.Gateway.GetAsync("/api/catalog/products")).StatusCode);
    }

    [Fact]
    public async Task AFailureTwoHopsAway_ComesBackAs503_WithRetryAfter()
    {
        _platform.OrdersToCatalog.Behavior = (_, _, _) => throw new HttpRequestException("catalog down");

        var response = await _platform.Gateway.PostAsJsonAsync("/api/orders/orders", NewOrder("ana@x.com", "PEN-1", 1));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task OneServiceDown_DoesNotBreakTheOthers()
    {
        _platform.GatewayToCatalog.Behavior = (_, _, _) => throw new HttpRequestException("catalog down");

        Assert.Equal(HttpStatusCode.BadGateway, (await _platform.Gateway.GetAsync("/api/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _platform.Gateway.GetAsync($"/api/orders/orders/{Guid.NewGuid()}")).StatusCode);
    }
}
