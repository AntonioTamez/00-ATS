namespace Ordering.Core;

internal enum OrderStatus { Placed, Cancelled }

internal sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

internal sealed class Order
{
    private readonly List<OrderLine> _lines;

    private Order(Guid id, string customerEmail, List<OrderLine> lines, DateTimeOffset placedAt)
    {
        Id = id;
        CustomerEmail = customerEmail;
        _lines = lines;
        PlacedAt = placedAt;
        Status = OrderStatus.Placed;
    }

    public Guid Id { get; }
    public string CustomerEmail { get; }
    public DateTimeOffset PlacedAt { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public decimal Total => _lines.Sum(l => l.UnitPrice * l.Quantity);

    /// <summary>Checks what can be checked before touching other modules (stock is theirs to judge).</summary>
    public static string? Validate(string? customerEmail, IReadOnlyCollection<(string Sku, int Quantity)> requested)
    {
        if (string.IsNullOrWhiteSpace(customerEmail) || !customerEmail.Contains('@'))
            return "A valid customer email is required.";
        if (requested.Count == 0)
            return "An order needs at least one line.";
        if (requested.Any(l => string.IsNullOrWhiteSpace(l.Sku) || l.Quantity <= 0))
            return "Every line needs a SKU and a positive quantity.";
        return null;
    }

    public static Order Place(string customerEmail, IEnumerable<OrderLine> lines, DateTimeOffset now) =>
        new(Guid.NewGuid(), customerEmail, lines.ToList(), now);

    public bool Cancel()
    {
        if (Status == OrderStatus.Cancelled) return false;
        Status = OrderStatus.Cancelled;
        return true;
    }
}
