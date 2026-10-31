using Catalog.Contracts;
using Catalog.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Shop.Tests;

/// <summary>Catalog alone: no other module, no host, no HTTP.</summary>
public class CatalogModuleTests
{
    private static ICatalogApi CreateCatalog()
    {
        var services = new ServiceCollection().AddCatalogModule().BuildServiceProvider();
        return services.CreateScope().ServiceProvider.GetRequiredService<ICatalogApi>();
    }

    [Fact]
    public async Task List_ReturnsSeededProducts_OrderedBySku()
    {
        var products = await CreateCatalog().ListAsync();

        Assert.Equal(["BOOK-1", "MUG-1", "PEN-1"], products.Select(p => p.Sku));
    }

    [Fact]
    public async Task Find_UnknownSku_ReturnsNull() =>
        Assert.Null(await CreateCatalog().FindAsync("NOPE"));

    [Fact]
    public async Task Reserve_DecreasesStock_AndReturnsUnitPrice()
    {
        var catalog = CreateCatalog();

        var result = await catalog.ReserveAsync("MUG-1", 2);

        Assert.True(result.Success);
        Assert.Equal(12.50m, result.UnitPrice);
        Assert.Equal(3, (await catalog.FindAsync("MUG-1"))!.Stock);
    }

    [Fact]
    public async Task Reserve_MoreThanStock_FailsAndKeepsStock()
    {
        var catalog = CreateCatalog();

        var result = await catalog.ReserveAsync("MUG-1", 6);

        Assert.False(result.Success);
        Assert.Contains("Not enough stock", result.Error);
        Assert.Equal(5, (await catalog.FindAsync("MUG-1"))!.Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Reserve_NonPositiveQuantity_Fails(int quantity) =>
        Assert.False((await CreateCatalog().ReserveAsync("MUG-1", quantity)).Success);

    [Fact]
    public async Task Reserve_UnknownSku_Fails() =>
        Assert.Contains("Unknown product", (await CreateCatalog().ReserveAsync("NOPE", 1)).Error);

    [Fact]
    public async Task Release_GivesStockBack()
    {
        var catalog = CreateCatalog();
        await catalog.ReserveAsync("BOOK-1", 4);

        await catalog.ReleaseAsync("BOOK-1", 4);

        Assert.Equal(10, (await catalog.FindAsync("BOOK-1"))!.Stock);
    }
}
