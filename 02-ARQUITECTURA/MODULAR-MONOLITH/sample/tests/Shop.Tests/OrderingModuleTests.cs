using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Contracts;
using Ordering.Core;
using Shop.SharedKernel;

namespace Shop.Tests;

/// <summary>Ordering alone: its only neighbour (Catalog) is replaced by a fake behind the public contract.</summary>
public class OrderingModuleTests
{
    private readonly FakeCatalog _catalog = new FakeCatalog().With("BOOK-1", 30m, 10).With("PEN-1", 2m, 3);
    private readonly RecordingBus _bus = new();

    private IOrderingApi CreateOrdering()
    {
        var provider = new ServiceCollection()
            .AddSingleton<ICatalogApi>(_catalog)
            .AddSingleton<IEventBus>(_bus)
            .AddSingleton(TimeProvider.System)
            .AddOrderingModule()
            .BuildServiceProvider();
        return provider.CreateScope().ServiceProvider.GetRequiredService<IOrderingApi>();
    }

    private static PlaceOrderCommand Command(string email, params (string Sku, int Qty)[] lines) =>
        new(email, lines.Select(l => new OrderLineRequest(l.Sku, l.Qty)).ToList());

    [Fact]
    public async Task Place_ComputesTotal_ReservesStock_AndPublishesOrderPlaced()
    {
        var result = await CreateOrdering().PlaceAsync(Command("ana@x.com", ("BOOK-1", 2), ("PEN-1", 1)));

        Assert.True(result.Success);
        Assert.Equal(62m, result.Order!.Total);
        Assert.Equal("Placed", result.Order.Status);
        Assert.Equal(8, _catalog.StockOf("BOOK-1"));
        var published = Assert.IsType<OrderPlaced>(Assert.Single(_bus.Published));
        Assert.Equal(result.Order.Id, published.OrderId);
        Assert.Equal(62m, published.Total);
        Assert.Equal("ana@x.com", published.CustomerEmail);
    }

    [Fact]
    public async Task Place_WhenALaterLineHasNoStock_ReleasesEarlierReservations_AndPublishesNothing()
    {
        var result = await CreateOrdering().PlaceAsync(Command("ana@x.com", ("BOOK-1", 2), ("PEN-1", 99)));

        Assert.False(result.Success);
        Assert.Contains("Not enough stock", result.Error);
        Assert.Equal(10, _catalog.StockOf("BOOK-1"));
        Assert.Empty(_bus.Published);
    }

    [Fact]
    public async Task Place_UnknownSku_Fails()
    {
        var result = await CreateOrdering().PlaceAsync(Command("ana@x.com", ("NOPE", 1)));

        Assert.False(result.Success);
        Assert.Contains("Unknown product", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Place_InvalidEmail_Fails_WithoutTouchingStock(string email)
    {
        var result = await CreateOrdering().PlaceAsync(Command(email, ("BOOK-1", 1)));

        Assert.False(result.Success);
        Assert.Equal(10, _catalog.StockOf("BOOK-1"));
    }

    [Fact]
    public async Task Place_WithoutLines_Fails() =>
        Assert.False((await CreateOrdering().PlaceAsync(Command("ana@x.com"))).Success);

    [Fact]
    public async Task Place_NonPositiveQuantity_Fails() =>
        Assert.False((await CreateOrdering().PlaceAsync(Command("ana@x.com", ("BOOK-1", 0)))).Success);

    [Fact]
    public async Task Get_ReturnsThePlacedOrder_AndNullForUnknownId()
    {
        var ordering = CreateOrdering();
        var placed = (await ordering.PlaceAsync(Command("ana@x.com", ("BOOK-1", 1)))).Order!;

        Assert.Equal(placed.Id, (await ordering.GetAsync(placed.Id))!.Id);
        Assert.Null(await ordering.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Cancel_ReleasesStock_MarksCancelled_AndPublishesOrderCancelled()
    {
        var ordering = CreateOrdering();
        var placed = (await ordering.PlaceAsync(Command("ana@x.com", ("BOOK-1", 3)))).Order!;

        var outcome = await ordering.CancelAsync(placed.Id);

        Assert.Equal(CancelOrderOutcome.Cancelled, outcome);
        Assert.Equal(10, _catalog.StockOf("BOOK-1"));
        Assert.Equal("Cancelled", (await ordering.GetAsync(placed.Id))!.Status);
        Assert.IsType<OrderCancelled>(_bus.Published.Last());
    }

    [Fact]
    public async Task Cancel_Twice_ReportsAlreadyCancelled_AndDoesNotReleaseStockTwice()
    {
        var ordering = CreateOrdering();
        var placed = (await ordering.PlaceAsync(Command("ana@x.com", ("BOOK-1", 3)))).Order!;
        await ordering.CancelAsync(placed.Id);

        var second = await ordering.CancelAsync(placed.Id);

        Assert.Equal(CancelOrderOutcome.AlreadyCancelled, second);
        Assert.Equal(10, _catalog.StockOf("BOOK-1"));
    }

    [Fact]
    public async Task Cancel_UnknownOrder_ReportsNotFound() =>
        Assert.Equal(CancelOrderOutcome.NotFound, await CreateOrdering().CancelAsync(Guid.NewGuid()));
}
