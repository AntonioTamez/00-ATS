using System.Net;
using System.Net.Http.Json;
using Catalog.Service;

namespace Platform.Tests;

/// <summary>The Catalog service alone, through its HTTP contract.</summary>
public class CatalogServiceTests : IDisposable
{
    private readonly Platform _platform = new();
    private HttpClient Http => _platform.Catalog;

    public void Dispose() => _platform.Dispose();

    private static object Reserve(Guid id, string sku, int quantity) => new { reservationId = id, sku, quantity };

    private async Task<int> StockAsync(string sku) =>
        (await Http.GetFromJsonAsync<ProductDto>($"/products/{sku}"))!.Stock;

    [Fact]
    public async Task Health_IsOk()
    {
        var response = await Http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("catalog", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Products_AreListedBySku()
    {
        var products = await Http.GetFromJsonAsync<List<ProductDto>>("/products");

        Assert.Equal(["BOOK-1", "MUG-1", "PEN-1"], products!.Select(p => p.Sku));
    }

    [Fact]
    public async Task UnknownProduct_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Http.GetAsync("/products/NOPE")).StatusCode);

    [Fact]
    public async Task Reserve_Returns201_WithThePrice_AndTakesTheStock()
    {
        var response = await Http.PostAsJsonAsync("/reservations", Reserve(Guid.NewGuid(), "MUG-1", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("9.5", await response.Content.ReadAsStringAsync());
        Assert.Equal(3, await StockAsync("MUG-1"));
    }

    [Fact]
    public async Task Reserve_Repeated_WithTheSameId_IsIdempotent()
    {
        var id = Guid.NewGuid();
        await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));

        var replay = await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);       // 200 instead of 201: "already done"
        Assert.Equal(3, await StockAsync("MUG-1"));                // taken once, not twice
    }

    [Fact]
    public async Task Reserve_ReusingAnIdForAnotherRequest_Returns409()
    {
        var id = Guid.NewGuid();
        await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));

        var response = await Http.PostAsJsonAsync("/reservations", Reserve(id, "PEN-1", 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("id_conflict", await response.Content.ReadAsStringAsync());
        Assert.Equal(100, await StockAsync("PEN-1"));
    }

    [Fact]
    public async Task Reserve_WithoutEnoughStock_Returns409_AndKeepsTheStock()
    {
        var response = await Http.PostAsJsonAsync("/reservations", Reserve(Guid.NewGuid(), "MUG-1", 6));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("out_of_stock", await response.Content.ReadAsStringAsync());
        Assert.Equal(5, await StockAsync("MUG-1"));
    }

    [Fact]
    public async Task Reserve_UnknownSku_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Http.PostAsJsonAsync("/reservations", Reserve(Guid.NewGuid(), "NOPE", 1))).StatusCode);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Reserve_NonPositiveQuantity_Returns400(int quantity) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.PostAsJsonAsync("/reservations", Reserve(Guid.NewGuid(), "MUG-1", quantity))).StatusCode);

    [Fact]
    public async Task Reserve_WithoutId_Returns400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Http.PostAsJsonAsync("/reservations", Reserve(Guid.Empty, "MUG-1", 1))).StatusCode);

    [Fact]
    public async Task Release_GivesTheStockBack_AndIsIdempotent()
    {
        var id = Guid.NewGuid();
        await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));

        var first = await Http.DeleteAsync($"/reservations/{id}");
        var second = await Http.DeleteAsync($"/reservations/{id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(5, await StockAsync("MUG-1"));                // returned once, not twice
    }

    [Fact]
    public async Task Release_UnknownReservation_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Http.DeleteAsync($"/reservations/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task ReleasedReservation_CannotBeReplayed()
    {
        var id = Guid.NewGuid();
        await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));
        await Http.DeleteAsync($"/reservations/{id}");

        var replay = await Http.PostAsJsonAsync("/reservations", Reserve(id, "MUG-1", 2));

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(5, await StockAsync("MUG-1"));
    }
}
