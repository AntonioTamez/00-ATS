using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Application;

/// <summary>Plain data that leaves the application layer. Aggregates never reach the API.</summary>
public sealed record RoomDto(Guid Id, string Number, decimal NightlyRate, string Currency)
{
    internal static RoomDto From(Room r) => new(r.Id.Value, r.Number, r.NightlyRate.Amount, r.NightlyRate.Currency);
}

public sealed record ChargeDto(Guid Id, string Description, decimal Amount);

public sealed record ReservationDto(
    Guid Id, Guid RoomId, string GuestName, string GuestEmail, DateOnly CheckIn, DateOnly CheckOut,
    int Nights, string Status, decimal RoomCharge, IReadOnlyList<ChargeDto> Charges, decimal Total, string Currency)
{
    internal static ReservationDto From(Reservation r) => new(
        r.Id.Value, r.RoomId.Value, r.Guest.Name, r.Guest.Email, r.Stay.CheckIn, r.Stay.CheckOut,
        r.Stay.Nights, r.Status.ToString(), r.RoomCharge.Amount,
        r.Charges.Select(c => new ChargeDto(c.Id, c.Description, c.Amount.Amount)).ToList(),
        r.Total.Amount, r.Total.Currency);
}
