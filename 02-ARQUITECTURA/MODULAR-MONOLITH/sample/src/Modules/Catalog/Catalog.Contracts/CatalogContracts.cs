namespace Catalog.Contracts;

public sealed record ProductInfo(string Sku, string Name, decimal Price, int Stock);

public sealed record ReservationResult(bool Success, decimal UnitPrice, string? Error)
{
    public static ReservationResult Ok(decimal unitPrice) => new(true, unitPrice, null);
    public static ReservationResult Fail(string error) => new(false, 0m, error);
}

/// <summary>The public face of the Catalog module. Other modules depend on this, never on Catalog.Core.</summary>
public interface ICatalogApi
{
    Task<IReadOnlyList<ProductInfo>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProductInfo?> FindAsync(string sku, CancellationToken cancellationToken = default);
    Task<ReservationResult> ReserveAsync(string sku, int quantity, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string sku, int quantity, CancellationToken cancellationToken = default);
}
