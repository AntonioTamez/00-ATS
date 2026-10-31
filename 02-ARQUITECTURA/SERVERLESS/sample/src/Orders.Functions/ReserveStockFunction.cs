using System.Text.Json;
using Faas.Runtime;

namespace Orders.Functions;

/// <summary>
/// Queue trigger on "orders". Reserves the stock of an order and announces the outcome on "order-events".
/// "Out of stock" is a business answer, not a failure: it is reported and the message is completed.
/// A malformed message or a missing order throws, so the platform retries it and finally dead-letters it.
/// </summary>
public sealed class ReserveStockFunction : IQueueFunction
{
    private readonly IOrderStore _store;

    public ReserveStockFunction(IOrderStore store) => _store = store;

    public async Task<Outputs> HandleAsync(QueueMessage message, FunctionContext context)
    {
        var orderId = ReadOrderId(message.Body);
        var order = await _store.FindAsync(orderId)
            ?? throw new InvalidOperationException($"order '{orderId}' not found");

        var result = await _store.ReserveOnceAsync(message.Id, orderId);
        return result switch
        {
            ReserveResult.Reserved => Outputs.Send(Event(message, "OrderReserved", order)),
            ReserveResult.OutOfStock => Outputs.Send(Event(message, "OrderRejected", order)),
            _ => Outputs.None,
        };
    }

    private static string ReadOrderId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("orderId", out var id) && id.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(id.GetString()))
                return id.GetString()!;
        }
        catch (JsonException)
        {
        }

        throw new InvalidOperationException("malformed message body");
    }

    private static OutgoingMessage Event(QueueMessage source, string type, Order order) =>
        new("order-events", JsonSerializer.Serialize(new { type, orderId = order.Id }), $"{source.Id}:{type}");
}
