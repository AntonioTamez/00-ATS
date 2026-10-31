namespace Gateway;

/// <summary>Lets the test project find this assembly. It carries no behavior.</summary>
public sealed class GatewayEntry;

internal static class Forwarding
{
    // /api/catalog/** -> catalog service, /api/orders/** -> orders service. Nothing else.
    private static readonly Dictionary<string, string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["catalog"] = "catalog",
        ["orders"] = "orders",
    };

    private static readonly string[] ForwardedRequestHeaders = ["Idempotency-Key", "Accept"];

    public static IEndpointRouteBuilder MapForwarding(this IEndpointRouteBuilder app)
    {
        app.Map("/api/{service}/{**path}", async (string service, string? path, HttpContext context, IHttpClientFactory factory) =>
        {
            if (!Routes.TryGetValue(service, out var clientName))
                return Results.NotFound(new { error = $"No route for '{service}'." });

            using var request = new HttpRequestMessage(
                new HttpMethod(context.Request.Method),
                "/" + path + context.Request.QueryString);

            foreach (var header in ForwardedRequestHeaders)
                if (context.Request.Headers.TryGetValue(header, out var values))
                    request.Headers.TryAddWithoutValidation(header, values.ToArray());

            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is { } contentType)
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }

            try
            {
                using var response = await factory.CreateClient(clientName)
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                context.Response.StatusCode = (int)response.StatusCode;
                if (response.Headers.RetryAfter is { } retryAfter)
                    context.Response.Headers.RetryAfter = retryAfter.ToString();
                if (response.Content.Headers.ContentType is { } type)
                    context.Response.ContentType = type.ToString();
                if (response.Headers.Location is { } location)
                    context.Response.Headers.Location = location.ToString();

                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
                return Results.Empty;
            }
            catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                return Results.Json(new { error = $"The {service} service timed out." }, statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (HttpRequestException)
            {
                return Results.Json(new { error = $"The {service} service is unreachable." }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        return app;
    }
}
