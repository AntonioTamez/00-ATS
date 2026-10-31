using System.Net;
using System.Net.Http.Json;

namespace Orders.Service;

internal enum ReserveStatus { Ok, UnknownProduct, OutOfStock, Rejected }

internal sealed record ReserveResult(ReserveStatus Status, decimal UnitPrice = 0m, string? Error = null);

/// <summary>The other service did not answer in a usable way, even after retrying.</summary>
internal sealed class CatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

internal interface ICatalogClient
{
    Task<ReserveResult> ReserveAsync(Guid reservationId, string sku, int quantity, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);
}

/// <summary>
/// Orders' view of Catalog's HTTP API. These DTOs are Orders' OWN copy of the contract: the services share
/// a protocol, not code. If Catalog changes the protocol in a breaking way, this is the only place that breaks.
/// </summary>
internal sealed class CatalogClient(IHttpClientFactory factory) : ICatalogClient
{
    private const int MaxAttempts = 2;

    private sealed record ReservedBody(decimal UnitPrice);
    private sealed record ErrorBody(string? Code, string? Error);

    public async Task<ReserveResult> ReserveAsync(Guid reservationId, string sku, int quantity, CancellationToken cancellationToken)
    {
        // Safe to retry: the reservation id makes the call idempotent on the Catalog side.
        using var response = await SendAsync(
            client => client.PostAsJsonAsync("/reservations", new { reservationId, sku, quantity }, cancellationToken),
            cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Created:
            case HttpStatusCode.OK:
                var body = await response.Content.ReadFromJsonAsync<ReservedBody>(cancellationToken);
                return new ReserveResult(ReserveStatus.Ok, body!.UnitPrice);
            case HttpStatusCode.NotFound:
                return new ReserveResult(ReserveStatus.UnknownProduct, Error: $"Unknown product {sku}.");
            case HttpStatusCode.Conflict:
                var conflict = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken);
                return conflict?.Code == "out_of_stock"
                    ? new ReserveResult(ReserveStatus.OutOfStock, Error: conflict.Error)
                    : new ReserveResult(ReserveStatus.Rejected, Error: conflict?.Error);
            default:
                return new ReserveResult(ReserveStatus.Rejected, Error: $"Catalog rejected the reservation ({(int)response.StatusCode}).");
        }
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        // Releasing is idempotent too; 404 just means "nothing was reserved", which is fine.
        using var response = await SendAsync(client => client.DeleteAsync($"/reservations/{reservationId}", cancellationToken), cancellationToken);
    }

    /// <summary>One retry for network failures, timeouts and 5xx. A real system adds backoff, jitter and a circuit breaker.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient("catalog");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await send(client);
                if ((int)response.StatusCode < 500) return response;

                response.Dispose();
                if (attempt == MaxAttempts) throw new CatalogUnavailableException($"Catalog answered {(int)response.StatusCode}.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt == MaxAttempts) throw new CatalogUnavailableException("Catalog is unreachable.", ex);
            }
        }
    }
}
