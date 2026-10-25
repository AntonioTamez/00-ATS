using System.Collections.Concurrent;

namespace Orders.Service;

/// <summary>Lets the test project find this assembly. It carries no behavior.</summary>
public sealed class OrdersServiceEntry;

public sealed record OrderLineRequest(string Sku, int Quantity);
public sealed record PlaceOrderRequest(string CustomerEmail, IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);
public sealed record Order(Guid Id, string CustomerEmail, IReadOnlyList<OrderLine> Lines, decimal Total, DateTimeOffset CreatedAt);

internal enum PlaceStatus { Placed, Rejected, CatalogUnavailable }

internal sealed record PlaceResult(PlaceStatus Status, Order? Order = null, string? Error = null);

/// <summary>This service's own data. Catalog cannot see it; Orders cannot see Catalog's stock.</summary>
internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();
    private readonly ConcurrentDictionary<string, Guid> _byIdempotencyKey = new();

    public void Save(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);

    public Order? FindByKey(string key) =>
        _byIdempotencyKey.TryGetValue(key, out var id) ? Find(id) : null;

    public void Remember(string key, Guid orderId) => _byIdempotencyKey[key] = orderId;
}

internal sealed class OrderService(ICatalogClient catalog, OrderStore store, TimeProvider clock)
{
    public async Task<PlaceResult> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request);
        if (invalid is not null) return new PlaceResult(PlaceStatus.Rejected, Error: invalid);

        var orderId = Guid.NewGuid();
        var attempted = new List<Guid>();     // every reservation we may have caused, even if the answer got lost
        var lines = new List<OrderLine>();

        try
        {
            foreach (var requested in request.Lines)
            {
                var reservationId = Guid.NewGuid();
                attempted.Add(reservationId);

                var result = await catalog.ReserveAsync(reservationId, requested.Sku, requested.Quantity, cancellationToken);
                if (result.Status != ReserveStatus.Ok)
                {
                    await CompensateAsync(attempted);
                    return new PlaceResult(PlaceStatus.Rejected, Error: result.Error);
                }

                lines.Add(new OrderLine(requested.Sku, requested.Quantity, result.UnitPrice));
            }
        }
        catch (CatalogUnavailableException ex)
        {
            await CompensateAsync(attempted);
            return new PlaceResult(PlaceStatus.CatalogUnavailable, Error: ex.Message);
        }

        var order = new Order(orderId, request.CustomerEmail.Trim(), lines, lines.Sum(l => l.UnitPrice * l.Quantity), clock.GetUtcNow());
        store.Save(order);
        return new PlaceResult(PlaceStatus.Placed, order);
    }

    /// <summary>There is no distributed transaction between services: we undo by calling the other service again.</summary>
    private async Task CompensateAsync(IEnumerable<Guid> reservationIds)
    {
        foreach (var id in reservationIds)
        {
            try
            {
                await catalog.ReleaseAsync(id, CancellationToken.None);
            }
            catch (CatalogUnavailableException)
            {
                // Best effort. In a real system this goes to a retry queue / reconciliation job (see the guide, limits).
            }
        }
    }

    private static string? Validate(PlaceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !request.CustomerEmail.Contains('@'))
            return "A valid customer email is required.";
        if (request.Lines is null || request.Lines.Count == 0)
            return "An order needs at least one line.";
        if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.Sku) || l.Quantity <= 0))
            return "Every line needs a SKU and a positive quantity.";
        return null;
    }
}
