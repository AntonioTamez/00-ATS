using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Tests;

/// <summary>The domain service owns the rule that spans aggregates: availability and pricing.</summary>
public class BookingServiceTests
{
    private readonly FakeReservations _reservations = new();
    private readonly Room _room = Room.Create("101", Samples.Eur(100m));
    private BookingService Service => new(_reservations);

    private static DateRange Stay(int from, int to) => new(Samples.Day(from), Samples.Day(to));

    private Task<Reservation> Book(int from, int to) =>
        Service.BookAsync(_room, Samples.Ana, Stay(from, to), Samples.Today, Samples.Now, default);

    [Fact]
    public async Task Book_PricesNightsTimesRate()
    {
        var reservation = await Book(5, 8);

        Assert.Equal(300m, reservation.RoomCharge.Amount);
    }

    [Fact]
    public async Task Book_LongStay_GetsTenPercentOff()
    {
        var reservation = await Book(5, 5 + BookingService.LongStayNights);   // 7 nights

        Assert.Equal(630m, reservation.RoomCharge.Amount);   // 700 - 10%
    }

    [Fact]
    public async Task Book_SixNights_NoDiscount()
    {
        var reservation = await Book(5, 11);

        Assert.Equal(600m, reservation.RoomCharge.Amount);
    }

    [Fact]
    public async Task Book_OverlappingActiveReservation_Throws()
    {
        await _reservations.AddAsync(await Book(5, 8), default);

        await Assert.ThrowsAsync<DomainException>(() => Book(7, 10));
    }

    [Fact]
    public async Task Book_BackToBack_IsAllowed()
    {
        await _reservations.AddAsync(await Book(5, 8), default);

        var next = await Book(8, 10);   // arrives the day the previous guest leaves

        Assert.Equal(200m, next.RoomCharge.Amount);
    }

    [Fact]
    public async Task Book_CancelledReservationDoesNotBlockTheRoom()
    {
        var first = await Book(5, 8);
        first.Cancel(Samples.Today, Samples.Now);
        await _reservations.AddAsync(first, default);

        var second = await Book(5, 8);

        Assert.Equal(ReservationStatus.Confirmed, second.Status);
    }

    [Fact]
    public async Task Book_OtherRoom_IsIndependent()
    {
        await _reservations.AddAsync(await Book(5, 8), default);
        var otherRoom = Room.Create("102", Samples.Eur(100m));

        var reservation = await Service.BookAsync(otherRoom, Samples.Ana, Stay(5, 8), Samples.Today, Samples.Now, default);

        Assert.Equal(otherRoom.Id, reservation.RoomId);
    }
}
