using System.Collections.Concurrent;
using Catalog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Core;

/// <summary>The ONLY public type of this assembly: the module's entry point for the host.</summary>
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddSingleton<ProductStore>();
        services.AddScoped<ICatalogApi, CatalogApi>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/catalog");

        group.MapGet("/products", async (ICatalogApi catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListAsync(ct)));

        group.MapGet("/products/{sku}", async (string sku, ICatalogApi catalog, CancellationToken ct) =>
            await catalog.FindAsync(sku, ct) is { } product ? Results.Ok(product) : Results.NotFound());

        return app;
    }
}

/// <summary>The module's own data. No other module can see this type, so nobody can read it directly.</summary>
internal sealed class ProductStore
{
    private readonly ConcurrentDictionary<string, Product> _products = new(StringComparer.OrdinalIgnoreCase);

    public ProductStore()
    {
        Add(new Product("BOOK-1", "Clean Architecture (book)", 30.00m, 10));
        Add(new Product("MUG-1", "Architecture mug", 12.50m, 5));
        Add(new Product("PEN-1", "Pen", 2.00m, 100));
    }

    public void Add(Product product) => _products[product.Sku] = product;

    public Product? Find(string sku) => _products.GetValueOrDefault(sku);

    public IReadOnlyList<Product> All() => _products.Values.OrderBy(p => p.Sku).ToList();
}

internal sealed class CatalogApi(ProductStore store) : ICatalogApi
{
    public Task<IReadOnlyList<ProductInfo>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductInfo>>(store.All().Select(ToInfo).ToList());

    public Task<ProductInfo?> FindAsync(string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Find(sku) is { } product ? ToInfo(product) : null);

    public Task<ReservationResult> ReserveAsync(string sku, int quantity, CancellationToken cancellationToken = default)
    {
        var product = store.Find(sku);
        if (product is null) return Task.FromResult(ReservationResult.Fail($"Unknown product {sku}."));

        var error = product.TryReserve(quantity);
        return Task.FromResult(error is null ? ReservationResult.Ok(product.Price) : ReservationResult.Fail(error));
    }

    public Task ReleaseAsync(string sku, int quantity, CancellationToken cancellationToken = default)
    {
        store.Find(sku)?.Release(quantity);
        return Task.CompletedTask;
    }

    private static ProductInfo ToInfo(Product p) => new(p.Sku, p.Name, p.Price, p.Stock);
}
