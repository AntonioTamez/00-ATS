using Shop.Domain.Model;
using Shop.Domain.Services;

namespace Shop.Application;

public sealed record PlaceOrderLine(string Sku, int Quantity, decimal UnitPrice);
public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderLine> Lines);

// Application services orchestrate use cases. They hold no business rules:
// those live in the model (entities) and in domain services, the rings inside.

public sealed class RegisterCustomerService(ICustomerRepository customers)
{
    public async Task<CustomerDto> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var customer = Customer.Create(name);
        await customers.AddAsync(customer, ct);
        return CustomerDto.From(customer);
    }
}

public sealed class PlaceOrderService(
    IOrderRepository orders, ICustomerRepository customers, OrderPlacementService placement, IClock clock)
{
    public async Task<OrderDto> ExecuteAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        var customer = await customers.GetByIdAsync(command.CustomerId, ct)
                       ?? throw new NotFoundException($"Customer '{command.CustomerId}' was not found.");

        var order = Order.Create(customer.Id);
        foreach (var line in command.Lines)
            order.AddLine(line.Sku, line.Quantity, new Money(line.UnitPrice));

        placement.Place(order, customer, clock.UtcNow);   // domain service: cross-entity rule

        await orders.AddAsync(order, ct);
        await customers.UpdateAsync(customer, ct);
        return OrderDto.From(order);
    }
}

public sealed class CancelOrderService(IOrderRepository orders)
{
    public async Task<OrderDto> ExecuteAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await orders.GetByIdAsync(orderId, ct)
                    ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        order.Cancel();
        await orders.UpdateAsync(order, ct);
        return OrderDto.From(order);
    }
}

public sealed class GetOrderService(IOrderRepository orders)
{
    public async Task<OrderDto> ExecuteAsync(Guid orderId, CancellationToken ct = default) =>
        OrderDto.From(await orders.GetByIdAsync(orderId, ct)
                      ?? throw new NotFoundException($"Order '{orderId}' was not found."));
}
