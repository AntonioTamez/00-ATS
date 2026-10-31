using Shop.SharedKernel;

namespace Ordering.Contracts;

public sealed record OrderLineRequest(string Sku, int Quantity);

public sealed record PlaceOrderCommand(string CustomerEmail, IReadOnlyList<OrderLineRequest> Lines);

public sealed record OrderLineDto(string Sku, int Quantity, decimal UnitPrice);

public sealed record OrderDto(
    Guid Id, string CustomerEmail, string Status, decimal Total, IReadOnlyList<OrderLineDto> Lines);

public sealed record PlaceOrderResult(bool Success, OrderDto? Order, string? Error)
{
    public static PlaceOrderResult Ok(OrderDto order) => new(true, order, null);
    public static PlaceOrderResult Fail(string error) => new(false, null, error);
}

public enum CancelOrderOutcome { Cancelled, NotFound, AlreadyCancelled }

/// <summary>The public face of the Ordering module.</summary>
public interface IOrderingApi
{
    Task<PlaceOrderResult> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default);
    Task<OrderDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<CancelOrderOutcome> CancelAsync(Guid orderId, CancellationToken cancellationToken = default);
}

// Integration events: the contract other modules react to. Keep them small and stable.
public sealed record OrderPlaced(
    Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, string CustomerEmail, decimal Total) : IIntegrationEvent;

public sealed record OrderCancelled(
    Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, string CustomerEmail) : IIntegrationEvent;
