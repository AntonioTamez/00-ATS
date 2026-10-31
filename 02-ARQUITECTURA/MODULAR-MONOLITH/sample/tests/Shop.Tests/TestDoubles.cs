using Catalog.Contracts;
using Shop.SharedKernel;

namespace Shop.Tests;

/// <summary>Stands in for the Catalog module when Ordering is tested alone.</summary>
internal sealed class FakeCatalog : ICatalogApi
{
    private readonly Dictionary<string, (decimal Price, int Stock)> _products = new(StringComparer.OrdinalIgnoreCase);

    public FakeCatalog With(string sku, decimal price, int stock)
    {
        _products[sku] = (price, stock);
        return this;
    }

    public int StockOf(string sku) => _products[sku].Stock;

    public Task<IReadOnlyList<ProductInfo>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductInfo>>(
            _products.Select(p => new ProductInfo(p.Key, p.Key, p.Value.Price, p.Value.Stock)).ToList());

    public Task<ProductInfo?> FindAsync(string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.TryGetValue(sku, out var p) ? new ProductInfo(sku, sku, p.Price, p.Stock) : null);

    public Task<ReservationResult> ReserveAsync(string sku, int quantity, CancellationToken cancellationToken = default)
    {
        if (!_products.TryGetValue(sku, out var p)) return Task.FromResult(ReservationResult.Fail($"Unknown product {sku}."));
        if (p.Stock < quantity) return Task.FromResult(ReservationResult.Fail($"Not enough stock for {sku}."));

        _products[sku] = (p.Price, p.Stock - quantity);
        return Task.FromResult(ReservationResult.Ok(p.Price));
    }

    public Task ReleaseAsync(string sku, int quantity, CancellationToken cancellationToken = default)
    {
        var p = _products[sku];
        _products[sku] = (p.Price, p.Stock + quantity);
        return Task.CompletedTask;
    }
}

internal sealed class RecordingBus : IEventBus
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        Published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
