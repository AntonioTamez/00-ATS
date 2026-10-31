namespace Shop.Domain.Model;

public enum OrderStatus { Draft, Placed, Cancelled }

public sealed record OrderLine(string Sku, int Quantity, Money UnitPrice)
{
    public Money Total => UnitPrice.Times(Quantity);
}

/// <summary>Entity: owns the rules of a single order. Cross-entity rules live in a domain service.</summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public OrderStatus Status { get; private set; }
    public Money Discount { get; private set; }
    public DateTimeOffset? PlacedAt { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public Money Subtotal => _lines.Aggregate(Money.Zero, (sum, l) => sum + l.Total);
    public Money Total => Subtotal - Discount;

    private Order(Guid id, Guid customerId)
    {
        Id = id;
        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public static Order Create(Guid customerId) => new(Guid.NewGuid(), customerId);

    /// <summary>Rebuilds a stored order. For infrastructure adapters, never to create new ones.</summary>
    public static Order Rehydrate(Guid id, Guid customerId, OrderStatus status, Money discount,
        DateTimeOffset? placedAt, IEnumerable<OrderLine> lines)
    {
        var order = new Order(id, customerId) { Status = status, Discount = discount, PlacedAt = placedAt };
        order._lines.AddRange(lines);
        return order;
    }

    public void AddLine(string sku, int quantity, Money unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Only a draft order can be modified.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("A product SKU is required.");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        _lines.Add(new OrderLine(sku.Trim(), quantity, unitPrice));
    }

    public void Place(Money discount, DateTimeOffset now)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Only a draft order can be placed.");
        if (_lines.Count == 0)
            throw new DomainException("An order needs at least one line.");
        if (discount.Amount > Subtotal.Amount)
            throw new DomainException("The discount cannot exceed the subtotal.");

        Discount = discount;
        PlacedAt = now;
        Status = OrderStatus.Placed;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("The order is already cancelled.");

        Status = OrderStatus.Cancelled;
    }
}
