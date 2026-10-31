using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Tests;

internal static class Samples
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 3, 1);
    public static DateOnly Day(int d) => new(2026, 3, d);
    public static readonly GuestInfo Ana = new("Ana", "ana@mail.com");
    public static Money Eur(decimal a) => new(a, "EUR");

    public static Reservation Booked(int checkIn = 5, int checkOut = 8, decimal charge = 300m) =>
        Reservation.Book(RoomId.New(), Ana, new DateRange(Day(checkIn), Day(checkOut)), Eur(charge), Today, Now);
}

/// <summary>The aggregate root protects its invariants: it can never be seen in an invalid state.</summary>
public class ReservationAggregateTests
{
    [Fact]
    public void Book_StartsConfirmed_AndRaisesReservationConfirmed()
    {
        var reservation = Samples.Booked();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        var confirmed = Assert.IsType<ReservationConfirmed>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(reservation.Id, confirmed.ReservationId);
        Assert.Equal("ana@mail.com", confirmed.GuestEmail);
        Assert.Equal(300m, confirmed.Total.Amount);
    }

    [Fact]
    public void Book_StartingInThePast_Throws() =>
        Assert.Throws<DomainException>(() => Reservation.Book(RoomId.New(), Samples.Ana,
            new DateRange(Samples.Day(1), Samples.Day(3)), Samples.Eur(100m), Samples.Day(2), Samples.Now));

    [Fact]
    public void Book_OverMaxNights_Throws() =>
        Assert.Throws<DomainException>(() => Reservation.Book(RoomId.New(), Samples.Ana,
            new DateRange(Samples.Day(1), Samples.Day(1).AddDays(Reservation.MaxNights + 1)),
            Samples.Eur(100m), Samples.Today, Samples.Now));

    [Fact]
    public void PullEvents_ReturnsEventsOnce()
    {
        var reservation = Samples.Booked();

        Assert.Single(reservation.PullEvents());
        Assert.Empty(reservation.PullEvents());
    }

    [Fact]
    public void Total_IsRoomChargePlusCharges()
    {
        var reservation = Samples.Booked(charge: 300m);

        reservation.AddCharge("Minibar", Samples.Eur(12.50m));
        reservation.AddCharge("Spa", Samples.Eur(40m));

        Assert.Equal(352.50m, reservation.Total.Amount);
        Assert.Equal(2, reservation.Charges.Count);
    }

    [Fact]
    public void AddCharge_InAnotherCurrency_Throws() =>
        Assert.Throws<DomainException>(() => Samples.Booked().AddCharge("Spa", new Money(10m, "USD")));

    [Fact]
    public void AddCharge_ZeroOrNoDescription_Throws()
    {
        var reservation = Samples.Booked();

        Assert.Throws<DomainException>(() => reservation.AddCharge("Spa", Samples.Eur(0m)));
        Assert.Throws<DomainException>(() => reservation.AddCharge(" ", Samples.Eur(5m)));
    }

    [Fact]
    public void CheckIn_OnlyDuringTheStay()
    {
        var reservation = Samples.Booked(checkIn: 5, checkOut: 8);

        Assert.Throws<DomainException>(() => reservation.CheckIn(Samples.Day(4)));   // too early
        Assert.Throws<DomainException>(() => reservation.CheckIn(Samples.Day(8)));   // departure day

        reservation.CheckIn(Samples.Day(5));
        Assert.Equal(ReservationStatus.CheckedIn, reservation.Status);
    }

    [Fact]
    public void CheckOut_RequiresCheckIn_AndRaisesGuestCheckedOut()
    {
        var reservation = Samples.Booked();
        reservation.PullEvents();

        Assert.Throws<DomainException>(() => reservation.CheckOut(Samples.Now));

        reservation.CheckIn(Samples.Day(5));
        reservation.AddCharge("Minibar", Samples.Eur(10m));
        reservation.CheckOut(Samples.Now);

        Assert.Equal(ReservationStatus.CheckedOut, reservation.Status);
        var evt = Assert.IsType<GuestCheckedOut>(Assert.Single(reservation.DomainEvents));
        Assert.Equal(310m, evt.Total.Amount);
    }

    [Fact]
    public void Cancel_BeforeCheckIn_RaisesReservationCancelled()
    {
        var reservation = Samples.Booked(checkIn: 5);
        reservation.PullEvents();

        reservation.Cancel(Samples.Day(4), Samples.Now);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.IsType<ReservationCancelled>(Assert.Single(reservation.DomainEvents));
    }

    [Fact]
    public void Cancel_OnOrAfterCheckInDate_Throws() =>
        Assert.Throws<DomainException>(() => Samples.Booked(checkIn: 5).Cancel(Samples.Day(5), Samples.Now));

    [Fact]
    public void Cancelled_CannotReceiveChargesOrBeCancelledTwice()
    {
        var reservation = Samples.Booked(checkIn: 5);
        reservation.Cancel(Samples.Day(2), Samples.Now);

        Assert.Throws<DomainException>(() => reservation.AddCharge("Spa", Samples.Eur(5m)));
        Assert.Throws<DomainException>(() => reservation.Cancel(Samples.Day(3), Samples.Now));
    }

    [Fact]
    public void Entities_AreEqualByIdentity_NotByAttributes()
    {
        var a = Samples.Booked();
        var b = Samples.Booked();   // same attributes, different identity

        Assert.NotEqual(a, b);
        Assert.Equal(a, a);
    }
}

public class RoomAggregateTests
{
    [Fact]
    public void Create_RequiresNumberAndPositiveRate()
    {
        Assert.Throws<DomainException>(() => Room.Create(" ", Samples.Eur(50m)));
        Assert.Throws<DomainException>(() => Room.Create("101", Samples.Eur(0m)));
    }

    [Fact]
    public void ChangeRate_CannotChangeCurrency()
    {
        var room = Room.Create("101", Samples.Eur(50m));

        Assert.Throws<DomainException>(() => room.ChangeRate(new Money(60m, "USD")));
        room.ChangeRate(Samples.Eur(60m));
        Assert.Equal(60m, room.NightlyRate.Amount);
    }
}
