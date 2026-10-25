using Hotel.Domain.Common;
using Hotel.Domain.Rooms;

namespace Hotel.Domain.Reservations;

public readonly record struct ReservationId(Guid Value) : IValueObject
{
    public static ReservationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum ReservationStatus { Confirmed, CheckedIn, CheckedOut, Cancelled }

// Domain events: immutable facts, named in the past tense, in the language of the business.
public sealed record ReservationConfirmed(ReservationId ReservationId, string GuestEmail, DateRange Stay, Money Total, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record ReservationCancelled(ReservationId ReservationId, string GuestEmail, DateTimeOffset OccurredAt) : IDomainEvent;
public sealed record GuestCheckedOut(ReservationId ReservationId, Money Total, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// A child entity: it has identity, but only inside its Reservation. It cannot exist alone,
/// so its constructor is internal and only the aggregate root creates it.
/// </summary>
public sealed class Charge : Entity<Guid>
{
    public string Description { get; private set; } = string.Empty;
    public Money Amount { get; private set; }

    internal Charge(string description, Money amount)
    {
        Id = Guid.NewGuid();
        Description = description;
        Amount = amount;
    }
}

/// <summary>
/// Aggregate root. Every change goes through a method that checks the invariants,
/// so a Reservation can never be observed in an invalid state.
/// References another aggregate (Room) by identity only.
/// </summary>
public sealed class Reservation : AggregateRoot<ReservationId>
{
    public const int MaxNights = 30;

    private readonly List<Charge> _charges = [];

    public RoomId RoomId { get; private set; }
    public GuestInfo Guest { get; private set; }
    public DateRange Stay { get; private set; }
    public ReservationStatus Status { get; private set; }
    public Money RoomCharge { get; private set; }
    public IReadOnlyList<Charge> Charges => _charges;
    public Money Total => _charges.Aggregate(RoomCharge, (sum, charge) => sum + charge.Amount);

    private Reservation() { }

    /// <summary>
    /// Factory. Availability and pricing are decided by <see cref="BookingService"/>
    /// because they involve other aggregates; this method protects what the reservation itself owns.
    /// </summary>
    public static Reservation Book(RoomId roomId, GuestInfo guest, DateRange stay, Money roomCharge,
        DateOnly today, DateTimeOffset now)
    {
        if (stay.Nights > MaxNights)
            throw new DomainException($"A stay cannot exceed {MaxNights} nights.");
        if (stay.CheckIn < today)
            throw new DomainException("Cannot book a stay that starts in the past.");

        var reservation = new Reservation
        {
            Id = ReservationId.New(),
            RoomId = roomId,
            Guest = guest,
            Stay = stay,
            RoomCharge = roomCharge,
            Status = ReservationStatus.Confirmed
        };

        reservation.Raise(new ReservationConfirmed(reservation.Id, guest.Email, stay, reservation.Total, now));
        return reservation;
    }

    public void AddCharge(string description, Money amount)
    {
        if (Status is not (ReservationStatus.Confirmed or ReservationStatus.CheckedIn))
            throw new DomainException("Charges can only be added to an active reservation.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("A charge description is required.");
        if (amount.Amount <= 0)
            throw new DomainException("A charge must be greater than zero.");
        if (amount.Currency != RoomCharge.Currency)
            throw new DomainException($"Charges must be in {RoomCharge.Currency}.");

        _charges.Add(new Charge(description.Trim(), amount));
    }

    public void CheckIn(DateOnly today)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("Only a confirmed reservation can check in.");
        if (!Stay.Includes(today))
            throw new DomainException("Check-in is only possible during the stay dates.");

        Status = ReservationStatus.CheckedIn;
    }

    public void CheckOut(DateTimeOffset now)
    {
        if (Status != ReservationStatus.CheckedIn)
            throw new DomainException("Only a checked-in reservation can check out.");

        Status = ReservationStatus.CheckedOut;
        Raise(new GuestCheckedOut(Id, Total, now));
    }

    public void Cancel(DateOnly today, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new DomainException("Only a confirmed reservation can be cancelled.");
        if (today >= Stay.CheckIn)
            throw new DomainException("Cancellation is only possible before the check-in date.");

        Status = ReservationStatus.Cancelled;
        Raise(new ReservationCancelled(Id, Guest.Email, now));
    }
}
