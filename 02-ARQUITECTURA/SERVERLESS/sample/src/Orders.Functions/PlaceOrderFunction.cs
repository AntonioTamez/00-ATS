using System.Text.Json;
using Faas.Runtime;

namespace Orders.Functions;

/// <summary>
/// POST /orders. Validates, stores the order and hands the slow part to a queue, so the caller gets an answer fast.
/// The client's <c>Idempotency-Key</c> header is the order id: sending the same request twice creates one order.
/// </summary>
public sealed class PlaceOrderFunction : IHttpFunction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOrderStore _store;

    public PlaceOrderFunction(IOrderStore store) => _store = store;

    public async Task<HttpResponse> HandleAsync(HttpRequest request, FunctionContext context)
    {
        var key = request.Header("Idempotency-Key");
        if (string.IsNullOrWhiteSpace(key))
            return Error(400, "Idempotency-Key header is required");

        PlaceOrderRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<PlaceOrderRequest>(request.Body, Json);
        }
        catch (JsonException)
        {
            return Error(400, "invalid JSON");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Customer) || string.IsNullOrWhiteSpace(body.Sku) || body.Quantity <= 0)
            return Error(422, "customer, sku and a positive quantity are required");

        var order = new Order(key.Trim(), body.Customer.Trim(), body.Sku.Trim().ToUpperInvariant(), body.Quantity);
        if (!await _store.TryAddAsync(order))
            return new HttpResponse(200, JsonSerializer.Serialize(new { orderId = order.Id, status = "already received" }));

        var message = new OutgoingMessage("orders", JsonSerializer.Serialize(new { orderId = order.Id }), order.Id);
        return new HttpResponse(202, JsonSerializer.Serialize(new { orderId = order.Id }), Outputs.Send(message));
    }

    private static HttpResponse Error(int status, string message) =>
        new(status, JsonSerializer.Serialize(new { error = message }));

    private sealed record PlaceOrderRequest(string? Customer, string? Sku, int Quantity);
}
