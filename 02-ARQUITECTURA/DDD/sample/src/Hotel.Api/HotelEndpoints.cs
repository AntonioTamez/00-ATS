using Hotel.Application;

namespace Hotel.Api;

public sealed record RegisterRoomRequest(string Number, decimal NightlyRate, string Currency = "EUR");
public sealed record BookRoomRequest(Guid RoomId, string GuestName, string GuestEmail, DateOnly CheckIn, DateOnly CheckOut);
public sealed record AddChargeRequest(string Description, decimal Amount);

/// <summary>Delivery mechanism: HTTP in, application service call, HTTP out. No business rules.</summary>
public static class HotelEndpoints
{
    public static IEndpointRouteBuilder MapHotelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/rooms", async (RegisterRoomRequest body, RegisterRoom useCase, CancellationToken ct) =>
        {
            var room = await useCase.ExecuteAsync(body.Number, body.NightlyRate, body.Currency, ct);
            return Results.Created($"/api/rooms/{room.Id}", room);
        });

        var reservations = app.MapGroup("/api/reservations");

        reservations.MapPost("/", async (BookRoomRequest body, BookRoom useCase, CancellationToken ct) =>
        {
            var reservation = await useCase.ExecuteAsync(
                new BookRoomCommand(body.RoomId, body.GuestName, body.GuestEmail, body.CheckIn, body.CheckOut), ct);
            return Results.Created($"/api/reservations/{reservation.Id}", reservation);
        });

        reservations.MapGet("/{id:guid}", async (Guid id, GetReservation useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(id, ct)));

        reservations.MapPost("/{id:guid}/cancel", async (Guid id, CancelReservation useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(id, ct)));

        reservations.MapPost("/{id:guid}/check-in", async (Guid id, CheckInGuest useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(id, ct)));

        reservations.MapPost("/{id:guid}/check-out", async (Guid id, CheckOutGuest useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(id, ct)));

        reservations.MapPost("/{id:guid}/charges", async (Guid id, AddChargeRequest body, AddCharge useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(new AddChargeCommand(id, body.Description, body.Amount), ct)));

        return app;
    }
}
