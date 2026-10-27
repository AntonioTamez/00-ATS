using Orders.Functions;

namespace Orders.Local;

/// <summary>
/// An in-memory adapter for <see cref="IOrderStore"/>. In a real deployment this would be a database; what matters
/// is that every operation is atomic, because the platform can run several invocations at the same time.
/// </summary>
public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Order> _orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _stock;
    private readonly HashSet<string> _processedMessages = new(StringComparer.Ordinal);

    public InMemoryOrderStore(IReadOnlyDictionary<string, int> stock) =>
        _stock = new Dictionary<string, int>(stock, StringComparer.OrdinalIgnoreCase);

    public int StockOf(string sku)
    {
        lock (_gate)
            return _stock.GetValueOrDefault(sku);
    }

    public Task<bool> TryAddAsync(Order order)
    {
        lock (_gate)
            return Task.FromResult(_orders.TryAdd(order.Id, order));
    }

    public Task<Order?> FindAsync(string orderId)
    {
        lock (_gate)
            return Task.FromResult(_orders.GetValueOrDefault(orderId));
    }

    public Task<ReserveResult> ReserveOnceAsync(string messageId, string orderId)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(orderId, out var order))
                throw new InvalidOperationException($"order '{orderId}' not found");

            if (_processedMessages.Contains(messageId) || order.Status != OrderStatus.Received)
                return Task.FromResult(ReserveResult.AlreadyProcessed);

            _processedMessages.Add(messageId);
            var available = _stock.GetValueOrDefault(order.Sku);
            if (available < order.Quantity)
            {
                _orders[orderId] = order with { Status = OrderStatus.OutOfStock };
                return Task.FromResult(ReserveResult.OutOfStock);
            }

            _stock[order.Sku] = available - order.Quantity;
            _orders[orderId] = order with { Status = OrderStatus.Reserved };
            return Task.FromResult(ReserveResult.Reserved);
        }
    }

    public Task<IReadOnlyList<Order>> ListAsync()
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<Order>>([.. _orders.Values.OrderBy(o => o.Id, StringComparer.Ordinal)]);
    }
}
