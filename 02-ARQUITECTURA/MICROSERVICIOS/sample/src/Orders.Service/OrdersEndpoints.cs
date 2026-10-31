namespace Orders.Service;

internal static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (PlaceOrderRequest request, HttpRequest http, OrderService orders, OrderStore store, CancellationToken ct) =>
        {
            // Idempotency-Key: if the client retries because it never saw our answer, it gets the same order back.
            var key = http.Headers["Idempotency-Key"].FirstOrDefault();
            if (key is not null && store.FindByKey(key) is { } existing)
                return Results.Ok(existing);

            var result = await orders.PlaceAsync(request, ct);
            switch (result.Status)
            {
                case PlaceStatus.Placed:
                    if (key is not null) store.Remember(key, result.Order!.Id);
                    return Results.Created($"/orders/{result.Order!.Id}", result.Order);
                case PlaceStatus.Rejected:
                    return Results.UnprocessableEntity(new { error = result.Error });
                default:
                    http.HttpContext.Response.Headers.RetryAfter = "5";
                    return Results.Json(new { error = "The order could not be placed because a dependency is unavailable.", detail = result.Error },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/orders/{id:guid}", (Guid id, OrderStore store) =>
            store.Find(id) is { } order ? Results.Ok(order) : Results.NotFound());

        return app;
    }
}
