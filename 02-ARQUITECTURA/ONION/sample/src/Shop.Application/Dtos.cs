using Shop.Domain.Model;

namespace Shop.Application;

/// <summary>Plain data that leaves the application ring. Entities never reach the UI.</summary>
public sealed record CustomerDto(Guid Id, string Name, bool IsLoyal, int CompletedOrders)
{
    internal static CustomerDto From(Customer c) => new(c.Id, c.Name, c.IsLoyal, c.CompletedOrders);
}

public sealed record OrderLineDto(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderDto(
    Guid Id, Guid CustomerId, string Status, IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal, decimal Discount, decimal Total, DateTimeOffset? PlacedAt)
{
    internal static OrderDto From(Order o) => new(
        o.Id, o.CustomerId, o.Status.ToString(),
        o.Lines.Select(l => new OrderLineDto(l.Sku, l.Quantity, l.UnitPrice.Amount, l.Total.Amount)).ToList(),
        o.Subtotal.Amount, o.Discount.Amount, o.Total.Amount, o.PlacedAt);
}
