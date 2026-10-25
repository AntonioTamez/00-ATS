using Hotel.Domain.Common;
using Hotel.Domain.Rooms;

namespace Hotel.Domain.Reservations;

/// <summary>
/// Domain service: a business rule that is not the natural responsibility of one entity.
/// "Is the room free?" involves Room and every other Reservation, so it belongs to neither.
/// Stateless, named in the ubiquitous language, and still free of infrastructure and use-case orchestration.
/// </summary>
public sealed class BookingService(IReservationRepository reservations)
{
    public const int LongStayNights = 7;
    public const decimal LongStayDiscountPercent = 10m;

    public Money PriceFor(Room room, DateRange stay)
    {
        var basePrice = room.NightlyRate.Times(stay.Nights);
        return stay.Nights >= LongStayNights ? basePrice - basePrice.Percent(LongStayDiscountPercent) : basePrice;
    }

    /// <summary>
    /// Checks availability and prices the stay. Note: the check and the later save are not one atomic step.
    /// Two concurrent bookings can both pass; a real store must back this rule with a constraint or
    /// optimistic concurrency (eventual consistency across aggregates).
    /// </summary>
    public async Task<Reservation> BookAsync(Room room, GuestInfo guest, DateRange stay,
        DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var clashes = await reservations.FindActiveOverlappingAsync(room.Id, stay, ct);
        if (clashes.Count > 0)
            throw new DomainException("The room is not available for those dates.");

        return Reservation.Book(room.Id, guest, stay, PriceFor(room, stay), today, now);
    }
}
