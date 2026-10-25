using System.Collections.Concurrent;
using Shop.Domain.Model;
using Shop.Domain.Services;

namespace Shop.Infrastructure;

/// <summary>Outer ring: implements interfaces owned by an inner ring. Swap for EF Core without touching the core.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _store = new();

    public Task AddAsync(Order order, CancellationToken ct) { _store[order.Id] = order; return Task.CompletedTask; }
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_store.GetValueOrDefault(id));
    public Task UpdateAsync(Order order, CancellationToken ct) { _store[order.Id] = order; return Task.CompletedTask; }
}

internal sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<Guid, Customer> _store = new();

    public Task AddAsync(Customer customer, CancellationToken ct) { _store[customer.Id] = customer; return Task.CompletedTask; }
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_store.GetValueOrDefault(id));
    public Task UpdateAsync(Customer customer, CancellationToken ct) { _store[customer.Id] = customer; return Task.CompletedTask; }
}
