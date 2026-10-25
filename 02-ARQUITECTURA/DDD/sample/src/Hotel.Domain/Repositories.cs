using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Domain;

/// <summary>
/// One repository per aggregate ROOT, enforced by the type constraint: you cannot
/// declare a repository for a child entity such as Charge. It behaves like a collection of whole aggregates.
/// Defined by the domain, implemented by infrastructure.
/// </summary>
public interface IRepository<TRoot, TId>
    where TRoot : AggregateRoot<TId>
    where TId : notnull
{
    Task AddAsync(TRoot root, CancellationToken ct);
    Task<TRoot?> GetByIdAsync(TId id, CancellationToken ct);
    Task UpdateAsync(TRoot root, CancellationToken ct);
}

public interface IRoomRepository : IRepository<Room, RoomId>
{
    Task<IReadOnlyList<Room>> ListAsync(CancellationToken ct);
}

public interface IReservationRepository : IRepository<Reservation, ReservationId>
{
    /// <summary>Active (confirmed or checked-in) reservations of a room whose stay overlaps the given range.</summary>
    Task<IReadOnlyList<Reservation>> FindActiveOverlappingAsync(RoomId roomId, DateRange stay, CancellationToken ct);
}
