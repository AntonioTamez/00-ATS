namespace Eda.Orders;

internal enum OrderStatus { Pending, Confirmed, Rejected, Cancelled }

internal sealed class Order
{
    private Order() { }

    public Guid Id { get; private init; }
    public string CustomerEmail { get; private init; } = "";
    public string Sku { get; private init; } = "";
    public int Quantity { get; private init; }
    public OrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    public static Order Place(string customerEmail, string sku, int quantity) =>
        new() { Id = Guid.NewGuid(), CustomerEmail = customerEmail.Trim(), Sku = sku.Trim(), Quantity = quantity, Status = OrderStatus.Pending };

    public static string? Validate(string? customerEmail, string? sku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(customerEmail) || !customerEmail.Contains('@')) return "A valid customer email is required.";
        if (string.IsNullOrWhiteSpace(sku)) return "A SKU is required.";
        if (quantity <= 0) return "The quantity must be positive.";
        return null;
    }

    // State changes return false when the order is not in a state where they make sense:
    // a late or repeated message must never move an order to a wrong state.
    public bool Confirm()
    {
        if (Status != OrderStatus.Pending) return false;
        Status = OrderStatus.Confirmed;
        return true;
    }

    public bool Reject(string reason)
    {
        if (Status != OrderStatus.Pending) return false;
        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        return true;
    }

    public bool Cancel()
    {
        if (Status != OrderStatus.Confirmed) return false;
        Status = OrderStatus.Cancelled;
        return true;
    }
}
