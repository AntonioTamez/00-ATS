using Hotel.Application;
using Hotel.Domain;
using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace Hotel.Tests;

/// <summary>
/// Application services: load one aggregate, call its behavior, save it, dispatch its events.
/// The dispatcher is the real one, so these tests also prove that domain events reach their handlers.
/// </summary>
public class UseCaseTests
{
    private readonly FakeRooms _rooms = new();
    private readonly FakeReservations _reservations = new();
    private readonly FakeClock _clock = new(Samples.Now);
    private readonly SpyNotifier _notifier = new();
    private readonly ServiceProvider _provider;

    public UseCaseTests()
    {
        _provider = new ServiceCollection()
            .AddSingleton<IRoomRepository>(_rooms)
            .AddSingleton<IReservationRepository>(_reservations)
            .AddSingleton<IClock>(_clock)
            .AddSingleton<INotifier>(_notifier)
            .AddApplication()
            .BuildServiceProvider();
    }

    private T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    private async Task<Guid> NewRoom(decimal rate = 100m) => (await Get<RegisterRoom>().ExecuteAsync("101", rate, "EUR")).Id;

    private Task<ReservationDto> Book(Guid roomId, int from = 5, int to = 8) =>
        Get<BookRoom>().ExecuteAsync(new BookRoomCommand(roomId, "Ana", "ana@mail.com", Samples.Day(from), Samples.Day(to)));

    [Fact]
    public async Task BookRoom_PersistsReservation_AndNotifiesGuestThroughTheEvent()
    {
        var roomId = await NewRoom();

        var reservation = await Book(roomId);

        Assert.Equal("Confirmed", reservation.Status);
        Assert.Equal(300m, reservation.Total);
        Assert.Single(_reservations.Items);
        var sent = Assert.Single(_notifier.Sent);
        Assert.Equal("ana@mail.com", sent.To);
        Assert.Equal("Reservation confirmed", sent.Subject);
    }

    [Fact]
    public async Task BookRoom_EventsAreDispatchedOnce()
    {
        var roomId = await NewRoom();
        await Book(roomId);

        var saved = _reservations.Items.Values.Single();

        Assert.Empty(saved.DomainEvents);   // pulled by the use case, not left to fire again
    }

    [Fact]
    public async Task BookRoom_UnknownRoom_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Book(Guid.NewGuid()));

    [Fact]
    public async Task BookRoom_InvalidGuest_ThrowsDomainException_AndSavesNothing()
    {
        var roomId = await NewRoom();

        await Assert.ThrowsAsync<DomainException>(() =>
            Get<BookRoom>().ExecuteAsync(new BookRoomCommand(roomId, "Ana", "nope", Samples.Day(5), Samples.Day(8))));

        Assert.Empty(_reservations.Items);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task BookRoom_RoomAlreadyTaken_Throws()
    {
        var roomId = await NewRoom();
        await Book(roomId, 5, 8);

        await Assert.ThrowsAsync<DomainException>(() => Book(roomId, 6, 9));
    }

    [Fact]
    public async Task CancelReservation_NotifiesGuest_AndFreesTheRoom()
    {
        var roomId = await NewRoom();
        var reservation = await Book(roomId);
        _notifier.Sent.Clear();

        var cancelled = await Get<CancelReservation>().ExecuteAsync(reservation.Id);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Reservation cancelled", Assert.Single(_notifier.Sent).Subject);
        await Book(roomId);   // the same dates are bookable again
    }

    [Fact]
    public async Task FullStay_CheckIn_Charge_CheckOut()
    {
        var roomId = await NewRoom();
        var reservation = await Book(roomId, 1, 4);   // starts today

        await Get<CheckInGuest>().ExecuteAsync(reservation.Id);
        await Get<AddCharge>().ExecuteAsync(new AddChargeCommand(reservation.Id, "Minibar", 12.5m));
        var done = await Get<CheckOutGuest>().ExecuteAsync(reservation.Id);

        Assert.Equal("CheckedOut", done.Status);
        Assert.Equal(312.5m, done.Total);
    }

    [Fact]
    public async Task CheckIn_BeforeTheStay_Throws()
    {
        var reservation = await Book(await NewRoom(), 5, 8);

        await Assert.ThrowsAsync<DomainException>(() => Get<CheckInGuest>().ExecuteAsync(reservation.Id));
    }

    [Fact]
    public async Task GetReservation_Unknown_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Get<GetReservation>().ExecuteAsync(Guid.NewGuid()));
}
