namespace Hotel.Domain.Common;

/// <summary>A business rule was violated. Raised by the model; never depends on HTTP or persistence.</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>Marker: defined by its attributes, immutable, no identity (DDD "Value Object").</summary>
public interface IValueObject;

/// <summary>Something that happened in the domain and that domain experts care about. Immutable, named in the past tense.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Defined by identity, not by attributes: two entities with the same id are the same thing.</summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Root of a consistency boundary. Outside code holds references to the root only;
/// the root enforces the invariants of everything inside and records the events it raises.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <summary>Hands the pending events to the caller and clears them, so each event is dispatched once.</summary>
    public IReadOnlyList<IDomainEvent> PullEvents()
    {
        var pending = _events.ToArray();
        _events.Clear();
        return pending;
    }
}
