namespace Catalog.Service;

/// <summary>Lets the test project find this assembly. It carries no behavior.</summary>
public sealed class CatalogServiceEntry;

internal enum ReserveStatus { Ok, UnknownSku, OutOfStock, InvalidRequest, IdConflict }

internal readonly record struct ReserveOutcome(ReserveStatus Status, decimal UnitPrice = 0m, bool Replayed = false);

internal enum ReleaseStatus { Released, AlreadyReleased, NotFound }

internal sealed class StockStore
{
    private sealed class Product(string sku, string name, decimal price, int stock)
    {
        public string Sku { get; } = sku;
        public string Name { get; } = name;
        public decimal Price { get; } = price;
        public int Stock { get; set; } = stock;
    }

    private sealed record Reservation(Guid Id, string Sku, int Quantity, decimal UnitPrice)
    {
        public bool Released { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Product> _products = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BOOK-1"] = new("BOOK-1", "Building Microservices (book)", 35.00m, 10),
        ["MUG-1"] = new("MUG-1", "Service mug", 9.50m, 5),
        ["PEN-1"] = new("PEN-1", "Pen", 1.50m, 100),
    };
    private readonly Dictionary<Guid, Reservation> _reservations = [];

    public IReadOnlyList<ProductDto> List()
    {
        lock (_gate) return _products.Values.OrderBy(p => p.Sku).Select(ToDto).ToList();
    }

    public ProductDto? Find(string sku)
    {
        lock (_gate) return _products.TryGetValue(sku, out var p) ? ToDto(p) : null;
    }

    /// <summary>
    /// Idempotent by reservation id: repeating the same request (same id, sku, quantity) returns the
    /// original answer without reserving twice. That is what makes a client retry safe.
    /// </summary>
    public ReserveOutcome Reserve(Guid reservationId, string sku, int quantity)
    {
        if (reservationId == Guid.Empty || string.IsNullOrWhiteSpace(sku) || quantity <= 0)
            return new ReserveOutcome(ReserveStatus.InvalidRequest);

        lock (_gate)
        {
            if (_reservations.TryGetValue(reservationId, out var existing))
            {
                var samePayload = string.Equals(existing.Sku, sku, StringComparison.OrdinalIgnoreCase) && existing.Quantity == quantity;
                return samePayload && !existing.Released
                    ? new ReserveOutcome(ReserveStatus.Ok, existing.UnitPrice, Replayed: true)
                    : new ReserveOutcome(ReserveStatus.IdConflict);
            }

            if (!_products.TryGetValue(sku, out var product)) return new ReserveOutcome(ReserveStatus.UnknownSku);
            if (product.Stock < quantity) return new ReserveOutcome(ReserveStatus.OutOfStock);

            product.Stock -= quantity;
            _reservations[reservationId] = new Reservation(reservationId, product.Sku, quantity, product.Price);
            return new ReserveOutcome(ReserveStatus.Ok, product.Price);
        }
    }

    /// <summary>Idempotent too: releasing twice gives the stock back only once.</summary>
    public ReleaseStatus Release(Guid reservationId)
    {
        lock (_gate)
        {
            if (!_reservations.TryGetValue(reservationId, out var reservation)) return ReleaseStatus.NotFound;
            if (reservation.Released) return ReleaseStatus.AlreadyReleased;

            reservation.Released = true;
            _products[reservation.Sku].Stock += reservation.Quantity;
            return ReleaseStatus.Released;
        }
    }

    private static ProductDto ToDto(Product p) => new(p.Sku, p.Name, p.Price, p.Stock);
}

public sealed record ProductDto(string Sku, string Name, decimal Price, int Stock);
