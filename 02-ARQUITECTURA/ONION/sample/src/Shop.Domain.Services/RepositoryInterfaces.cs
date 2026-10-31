using Shop.Domain.Model;

namespace Shop.Domain.Services;

// Palermo: "Inner layers define interfaces. Outer layers implement interfaces."
// The first ring around the model holds the repository interfaces; Infrastructure implements them.

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Order order, CancellationToken ct);
}

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Customer customer, CancellationToken ct);
}
