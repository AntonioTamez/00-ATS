namespace Orders.Functions;

public enum OrderStatus { Received, Reserved, OutOfStock }

public sealed record Order(string Id, string Customer, string Sku, int Quantity, OrderStatus Status = OrderStatus.Received);

public enum ReserveResult { Reserved, OutOfStock, AlreadyProcessed }

/// <summary>
/// The functions keep NO state of their own: everything that must survive between invocations lives behind this port.
/// </summary>
public interface IOrderStore
{
    /// <summary>Stores the order. False when an order with that id already exists.</summary>
    Task<bool> TryAddAsync(Order order);

    Task<Order?> FindAsync(string orderId);

    /// <summary>
    /// Reserves the order's stock, at most once per <paramref name="messageId"/> and once per order, in one atomic step.
    /// This is where idempotency lives: the platform delivers at least once, so the same message can arrive twice.
    /// </summary>
    Task<ReserveResult> ReserveOnceAsync(string messageId, string orderId);

    Task<IReadOnlyList<Order>> ListAsync();
}
