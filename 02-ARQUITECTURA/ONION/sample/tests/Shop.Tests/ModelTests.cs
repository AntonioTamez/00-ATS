using Shop.Domain.Model;
using Shop.Domain.Services;

namespace Shop.Tests;

/// <summary>Ring 1 (model): pure unit tests, nothing to mock.</summary>
public class ModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Money_RejectsNegativeAmounts() => Assert.Throws<DomainException>(() => new Money(-1m));

    [Fact]
    public void Money_Percent_RoundsToCents() => Assert.Equal(3.33m, new Money(33.33m).Percent(10m).Amount);

    [Fact]
    public void Order_Total_IsSubtotalMinusDiscount()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddLine("A", 2, new Money(50m));
        order.Place(new Money(10m), Now);

        Assert.Equal(100m, order.Subtotal.Amount);
        Assert.Equal(90m, order.Total.Amount);
    }

    [Fact]
    public void Order_CannotBePlacedWithoutLines() =>
        Assert.Throws<DomainException>(() => Order.Create(Guid.NewGuid()).Place(Money.Zero, Now));

    [Fact]
    public void Order_RejectsNonPositiveQuantity() =>
        Assert.Throws<DomainException>(() => Order.Create(Guid.NewGuid()).AddLine("A", 0, new Money(1m)));

    [Fact]
    public void Order_PlacedCannotBeModified()
    {
        var order = Order.Create(Guid.NewGuid());
        order.AddLine("A", 1, new Money(5m));
        order.Place(Money.Zero, Now);

        Assert.Throws<DomainException>(() => order.AddLine("B", 1, new Money(1m)));
    }

    [Fact]
    public void Order_CancelTwice_Throws()
    {
        var order = Order.Create(Guid.NewGuid());
        order.Cancel();

        Assert.Throws<DomainException>(order.Cancel);
    }

    [Fact]
    public void Customer_BecomesLoyalAfterThreeOrders()
    {
        var customer = Customer.Create("Ana");
        for (var i = 0; i < Customer.LoyaltyThreshold; i++) customer.RegisterPlacedOrder();

        Assert.True(customer.IsLoyal);
    }
}

/// <summary>Ring 2 (domain services): rules that span several entities.</summary>
public class DomainServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly OrderPlacementService _service = new();

    private static Order OrderOf(Customer c, decimal unitPrice, int qty = 1)
    {
        var order = Order.Create(c.Id);
        order.AddLine("SKU", qty, new Money(unitPrice));
        return order;
    }

    [Fact]
    public void SmallOrder_NewCustomer_NoDiscount()
    {
        var c = Customer.Create("Ana");
        Assert.Equal(0m, _service.CalculateDiscount(OrderOf(c, 50m), c).Amount);
    }

    [Fact]
    public void BulkOrder_Gets10Percent()
    {
        var c = Customer.Create("Ana");
        Assert.Equal(10m, _service.CalculateDiscount(OrderOf(c, 100m), c).Amount);
    }

    [Fact]
    public void LoyalCustomer_BulkOrder_Gets15Percent()
    {
        var c = Customer.Create("Ana");
        for (var i = 0; i < Customer.LoyaltyThreshold; i++) c.RegisterPlacedOrder();

        Assert.Equal(15m, _service.CalculateDiscount(OrderOf(c, 100m), c).Amount);
    }

    [Fact]
    public void Place_AppliesDiscountAndCountsOrderForCustomer()
    {
        var c = Customer.Create("Ana");
        var order = OrderOf(c, 200m);

        _service.Place(order, c, Now);

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(180m, order.Total.Amount);
        Assert.Equal(1, c.CompletedOrders);
    }

    [Fact]
    public void Place_OrderOfAnotherCustomer_Throws()
    {
        var owner = Customer.Create("Ana");
        var other = Customer.Create("Luis");

        Assert.Throws<DomainException>(() => _service.Place(OrderOf(owner, 10m), other, Now));
    }
}
