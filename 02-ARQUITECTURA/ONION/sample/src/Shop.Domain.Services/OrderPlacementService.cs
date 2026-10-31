using Shop.Domain.Model;

namespace Shop.Domain.Services;

/// <summary>
/// Domain service: business logic that spans more than one entity (Order + Customer),
/// so it fits neither inside Order nor inside Customer. It still knows nothing about use cases or I/O.
/// </summary>
public sealed class OrderPlacementService
{
    public const decimal BulkThreshold = 100m;
    public const decimal BulkDiscountPercent = 10m;
    public const decimal LoyaltyDiscountPercent = 5m;

    public Money CalculateDiscount(Order order, Customer customer)
    {
        var percent = 0m;
        if (order.Subtotal.Amount >= BulkThreshold) percent += BulkDiscountPercent;
        if (customer.IsLoyal) percent += LoyaltyDiscountPercent;
        return order.Subtotal.Percent(percent);
    }

    public void Place(Order order, Customer customer, DateTimeOffset now)
    {
        if (order.CustomerId != customer.Id)
            throw new DomainException("The order does not belong to this customer.");

        order.Place(CalculateDiscount(order, customer), now);
        customer.RegisterPlacedOrder();
    }
}
