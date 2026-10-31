using Hotel.Domain.Common;

namespace Hotel.Domain.Rooms;

/// <summary>Strongly typed identity: a RoomId can never be confused with a ReservationId.</summary>
public readonly record struct RoomId(Guid Value) : IValueObject
{
    public static RoomId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// An aggregate on its own. It does NOT hold its reservations: they have their own lifecycle,
/// so keeping them out keeps the aggregate small and avoids contention on one lock.
/// </summary>
public sealed class Room : AggregateRoot<RoomId>
{
    public string Number { get; private set; } = string.Empty;
    public Money NightlyRate { get; private set; }

    private Room() { }   // for persistence adapters; the factory is the only public way in

    public static Room Create(string number, Money nightlyRate)
    {
        var normalized = number?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new DomainException("A room number is required.");
        if (nightlyRate.Amount <= 0)
            throw new DomainException("A nightly rate must be greater than zero.");

        return new Room { Id = RoomId.New(), Number = normalized, NightlyRate = nightlyRate };
    }

    public void ChangeRate(Money newRate)
    {
        if (newRate.Amount <= 0)
            throw new DomainException("A nightly rate must be greater than zero.");
        if (newRate.Currency != NightlyRate.Currency)
            throw new DomainException("The rate currency cannot change.");

        NightlyRate = newRate;
    }
}
