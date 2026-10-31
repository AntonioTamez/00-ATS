using Shop.Application;
using Shop.Domain.Model;
using Shop.Domain.Services;

namespace Shop.Tests;

/// <summary>Ring 3 (application services): tested with hand-written fakes of the interfaces owned by inner rings.</summary>
public class ApplicationServiceTests
{
    private sealed class FakeClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }

    private sealed class FakeOrders : IOrderRepository
    {
        public Dictionary<Guid, Order> Items { get; } = new();
        public Task AddAsync(Order o, CancellationToken ct) { Items[o.Id] = o; return Task.CompletedTask; }
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task UpdateAsync(Order o, CancellationToken ct) { Items[o.Id] = o; return Task.CompletedTask; }
    }

    private sealed class FakeCustomers : ICustomerRepository
    {
        public Dictionary<Guid, Customer> Items { get; } = new();
        public Task AddAsync(Customer c, CancellationToken ct) { Items[c.Id] = c; return Task.CompletedTask; }
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task UpdateAsync(Customer c, CancellationToken ct) { Items[c.Id] = c; return Task.CompletedTask; }
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeOrders _orders = new();
    private readonly FakeCustomers _customers = new();

    private PlaceOrderService PlaceService() =>
        new(_orders, _customers, new OrderPlacementService(), new FakeClock(Now));

    [Fact]
    public async Task PlaceOrder_AppliesDomainServiceDiscount_AndPersists()
    {
        var customer = await new RegisterCustomerService(_customers).ExecuteAsync("Ana");

        var order = await PlaceService().ExecuteAsync(
            new PlaceOrderCommand(customer.Id, [new PlaceOrderLine("SKU", 2, 100m)]));

        Assert.Equal("Placed", order.Status);
        Assert.Equal(20m, order.Discount);
        Assert.Equal(180m, order.Total);
        Assert.Single(_orders.Items);
        Assert.Equal(1, _customers.Items[customer.Id].CompletedOrders);
    }

    [Fact]
    public async Task PlaceOrder_UnknownCustomer_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            PlaceService().ExecuteAsync(new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderLine("A", 1, 1m)])));

    [Fact]
    public async Task PlaceOrder_WithoutLines_ThrowsDomainException_AndPersistsNothing()
    {
        var customer = await new RegisterCustomerService(_customers).ExecuteAsync("Ana");

        await Assert.ThrowsAsync<DomainException>(() =>
            PlaceService().ExecuteAsync(new PlaceOrderCommand(customer.Id, [])));

        Assert.Empty(_orders.Items);
    }

    [Fact]
    public async Task CancelOrder_MarksOrderCancelled()
    {
        var customer = await new RegisterCustomerService(_customers).ExecuteAsync("Ana");
        var placed = await PlaceService().ExecuteAsync(
            new PlaceOrderCommand(customer.Id, [new PlaceOrderLine("A", 1, 10m)]));

        var cancelled = await new CancelOrderService(_orders).ExecuteAsync(placed.Id);

        Assert.Equal("Cancelled", cancelled.Status);
    }

    [Fact]
    public async Task GetOrder_Unknown_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new GetOrderService(_orders).ExecuteAsync(Guid.NewGuid()));
}
