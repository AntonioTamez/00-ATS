using System.Net;
using System.Net.Http.Json;
using Catalog.Service;
using Orders.Service;

namespace Platform.Tests;

/// <summary>Orders talking to a real Catalog over a link we can break.</summary>
public class OrdersServiceTests : IDisposable
{
    private readonly Platform _platform = new();

    public void Dispose() => _platform.Dispose();

    private static object NewOrder(string email, params (string Sku, int Quantity)[] lines) =>
        new { customerEmail = email, lines = lines.Select(l => new { sku = l.Sku, quantity = l.Quantity }) };

    private async Task<int> StockAsync(string sku) =>
        (await _platform.Catalog.GetFromJsonAsync<ProductDto>($"/products/{sku}"))!.Stock;

    [Fact]
    public async Task Health_IsOk() =>
        Assert.Equal(HttpStatusCode.OK, (await _platform.Orders.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task PlacingAnOrder_ReservesStockInCatalog_AndComputesTheTotal()
    {
        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("BOOK-1", 2), ("PEN-1", 4)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<Orders.Service.Order>())!;
        Assert.Equal(76m, order.Total);                                  // 2 x 35.00 + 4 x 1.50
        Assert.Equal($"/orders/{order.Id}", response.Headers.Location!.ToString());
        Assert.Equal(8, await StockAsync("BOOK-1"));
        Assert.Equal(96, await StockAsync("PEN-1"));
    }

    [Fact]
    public async Task APlacedOrder_CanBeFetched_AndAnUnknownOneIs404()
    {
        var created = await (await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("PEN-1", 1))))
            .Content.ReadFromJsonAsync<Orders.Service.Order>();

        Assert.Equal(HttpStatusCode.OK, (await _platform.Orders.GetAsync($"/orders/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _platform.Orders.GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task WhenALaterLineHasNoStock_TheEarlierReservationsAreUndone()
    {
        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("BOOK-1", 3), ("MUG-1", 99)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Not enough stock", await response.Content.ReadAsStringAsync());
        Assert.Equal(10, await StockAsync("BOOK-1"));                    // compensated over HTTP
        Assert.Equal(5, await StockAsync("MUG-1"));
    }

    [Fact]
    public async Task UnknownProduct_IsRejectedWith422()
    {
        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("NOPE", 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Unknown product", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task InvalidEmail_Returns422_WithoutCallingCatalog(string email)
    {
        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder(email, ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(_platform.OrdersToCatalog.Calls);
    }

    [Fact]
    public async Task OrderWithoutLines_Returns422() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com"))).StatusCode);

    // ---------- failures between services ----------

    [Fact]
    public async Task WhenCatalogIsDown_Returns503_WithRetryAfter_AndCreatesNoOrder()
    {
        _platform.OrdersToCatalog.Behavior = (_, _, _) => throw new HttpRequestException("connection refused");

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString());
        Assert.Equal(100, await StockAsync("PEN-1"));
    }

    [Fact]
    public async Task WhenCatalogTimesOut_Returns503()
    {
        _platform.OrdersToCatalog.Behavior = (_, _, _) => throw new TaskCanceledException("timeout");

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ACatalogThatKeepsFailingWith500_IsRetriedOnce_ThenGivenUpOn()
    {
        _platform.OrdersToCatalog.Behavior = (_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(2, _platform.OrdersToCatalog.Calls.Count(c => c == "POST /reservations"));
    }

    [Fact]
    public async Task ATransientFailure_IsRetried_AndTheOrderStillSucceeds()
    {
        _platform.OrdersToCatalog.Behavior = (_, next, call) =>
            call == 1 ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)) : next();

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(99, await StockAsync("PEN-1"));
    }

    [Fact]
    public async Task WhenTheReplyIsLost_TheRetryDoesNotReserveTwice_BecauseTheReservationIsIdempotent()
    {
        // Catalog does the work, but the answer never reaches Orders (the classic distributed-systems trap).
        _platform.OrdersToCatalog.Behavior = async (_, next, call) =>
        {
            var response = await next();
            if (call == 1) throw new HttpRequestException("connection reset while reading the response");
            return response;
        };

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("MUG-1", 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, await StockAsync("MUG-1"));                      // reserved once, not twice
    }

    [Fact]
    public async Task WhenTheReplyIsLostAndCatalogStaysDown_TheOrphanReservationIsReleasedWhenPossible()
    {
        // First request reaches Catalog (stock taken) but the answer is lost; the retry also fails; the release works.
        _platform.OrdersToCatalog.Behavior = async (request, next, call) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                if (call == 1) { await next(); }
                throw new HttpRequestException("network down");
            }
            return await next();                                          // the compensation (DELETE) gets through
        };

        var response = await _platform.Orders.PostAsJsonAsync("/orders", NewOrder("ana@x.com", ("MUG-1", 2)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(5, await StockAsync("MUG-1"));                      // nothing stays reserved
    }

    // ---------- idempotency on the public API ----------

    [Fact]
    public async Task RepeatingARequestWithTheSameIdempotencyKey_ReturnsTheSameOrder_WithoutReservingAgain()
    {
        async Task<HttpResponseMessage> PostAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = JsonContent.Create(NewOrder("ana@x.com", ("MUG-1", 1))) };
            request.Headers.Add("Idempotency-Key", "checkout-42");
            return await _platform.Orders.SendAsync(request);
        }

        var first = await PostAsync();
        var second = await PostAsync();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<Orders.Service.Order>())!.Id,
            (await second.Content.ReadFromJsonAsync<Orders.Service.Order>())!.Id);
        Assert.Equal(4, await StockAsync("MUG-1"));
    }
}
