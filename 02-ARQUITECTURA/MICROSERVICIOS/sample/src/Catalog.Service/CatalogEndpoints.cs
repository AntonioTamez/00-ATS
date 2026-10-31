namespace Catalog.Service;

public sealed record ReserveRequest(Guid ReservationId, string Sku, int Quantity);

internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", (StockStore store) => Results.Ok(store.List()));

        app.MapGet("/products/{sku}", (string sku, StockStore store) =>
            store.Find(sku) is { } product ? Results.Ok(product) : Results.NotFound());

        app.MapPost("/reservations", (ReserveRequest request, StockStore store) =>
        {
            var outcome = store.Reserve(request.ReservationId, request.Sku, request.Quantity);
            return outcome.Status switch
            {
                ReserveStatus.Ok => Results.Json(
                    new { request.ReservationId, request.Sku, request.Quantity, outcome.UnitPrice },
                    statusCode: outcome.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created),
                ReserveStatus.UnknownSku => Results.NotFound(new { code = "unknown_sku", error = $"Unknown product {request.Sku}." }),
                ReserveStatus.OutOfStock => Results.Conflict(new { code = "out_of_stock", error = $"Not enough stock for {request.Sku}." }),
                ReserveStatus.IdConflict => Results.Conflict(new { code = "id_conflict", error = "The reservation id was already used for a different request." }),
                _ => Results.BadRequest(new { code = "invalid_request", error = "A reservation id, a SKU and a positive quantity are required." }),
            };
        });

        app.MapDelete("/reservations/{id:guid}", (Guid id, StockStore store) =>
            store.Release(id) == ReleaseStatus.NotFound ? Results.NotFound() : Results.NoContent());

        return app;
    }
}
