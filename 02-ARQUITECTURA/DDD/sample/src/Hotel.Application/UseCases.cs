using Hotel.Domain;
using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Application;

// Application services: orchestrate one use case. They hold NO business rules.
// Pattern: load ONE aggregate -> call its behavior -> save it -> dispatch its events.
// One transaction changes one aggregate; other aggregates react through domain events.

public sealed record BookRoomCommand(Guid RoomId, string GuestName, string GuestEmail, DateOnly CheckIn, DateOnly CheckOut);
public sealed record AddChargeCommand(Guid ReservationId, string Description, decimal Amount);

public sealed class RegisterRoom(IRoomRepository rooms)
{
    public async Task<RoomDto> ExecuteAsync(string number, decimal nightlyRate, string currency, CancellationToken ct = default)
    {
        var room = Room.Create(number, new Money(nightlyRate, currency));
        await rooms.AddAsync(room, ct);
        return RoomDto.From(room);
    }
}

public sealed class BookRoom(
    IRoomRepository rooms, IReservationRepository reservations, BookingService booking,
    IClock clock, DomainEventDispatcher events)
{
    public async Task<ReservationDto> ExecuteAsync(BookRoomCommand command, CancellationToken ct = default)
    {
        var roomId = new RoomId(command.RoomId);
        var room = await rooms.GetByIdAsync(roomId, ct)
                   ?? throw new NotFoundException($"Room '{command.RoomId}' was not found.");

        var reservation = await booking.BookAsync(
            room,
            new GuestInfo(command.GuestName, command.GuestEmail),
            new DateRange(command.CheckIn, command.CheckOut),
            clock.Today, clock.UtcNow, ct);

        await reservations.AddAsync(reservation, ct);
        await events.DispatchAsync(reservation.PullEvents(), ct);
        return ReservationDto.From(reservation);
    }
}

public sealed class CancelReservation(IReservationRepository reservations, IClock clock, DomainEventDispatcher events)
{
    public async Task<ReservationDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await reservations.LoadAsync(id, ct);
        reservation.Cancel(clock.Today, clock.UtcNow);
        await reservations.UpdateAsync(reservation, ct);
        await events.DispatchAsync(reservation.PullEvents(), ct);
        return ReservationDto.From(reservation);
    }
}

internal static class ReservationLookup
{
    public static async Task<Reservation> LoadAsync(this IReservationRepository repo, Guid id, CancellationToken ct) =>
        await repo.GetByIdAsync(new ReservationId(id), ct)
        ?? throw new NotFoundException($"Reservation '{id}' was not found.");
}

public sealed class CheckInGuest(IReservationRepository reservations, IClock clock)
{
    public async Task<ReservationDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await reservations.LoadAsync(id, ct);
        reservation.CheckIn(clock.Today);
        await reservations.UpdateAsync(reservation, ct);
        return ReservationDto.From(reservation);
    }
}

public sealed class CheckOutGuest(IReservationRepository reservations, IClock clock, DomainEventDispatcher events)
{
    public async Task<ReservationDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await reservations.LoadAsync(id, ct);
        reservation.CheckOut(clock.UtcNow);
        await reservations.UpdateAsync(reservation, ct);
        await events.DispatchAsync(reservation.PullEvents(), ct);
        return ReservationDto.From(reservation);
    }
}

public sealed class AddCharge(IReservationRepository reservations)
{
    public async Task<ReservationDto> ExecuteAsync(AddChargeCommand command, CancellationToken ct = default)
    {
        var reservation = await reservations.LoadAsync(command.ReservationId, ct);
        reservation.AddCharge(command.Description, new Money(command.Amount, reservation.RoomCharge.Currency));
        await reservations.UpdateAsync(reservation, ct);
        return ReservationDto.From(reservation);
    }
}

public sealed class GetReservation(IReservationRepository reservations)
{
    public async Task<ReservationDto> ExecuteAsync(Guid id, CancellationToken ct = default) =>
        ReservationDto.From(await reservations.LoadAsync(id, ct));
}
