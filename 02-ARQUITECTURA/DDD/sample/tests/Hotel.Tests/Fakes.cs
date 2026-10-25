using Hotel.Application;
using Hotel.Domain;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;

namespace Hotel.Tests;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class SpyNotifier : INotifier
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class FakeRooms : IRoomRepository
{
    public Dictionary<RoomId, Room> Items { get; } = new();
    public Task AddAsync(Room root, CancellationToken ct) { Items[root.Id] = root; return Task.CompletedTask; }
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
    public Task UpdateAsync(Room root, CancellationToken ct) { Items[root.Id] = root; return Task.CompletedTask; }
    public Task<IReadOnlyList<Room>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Room>>(Items.Values.ToList());
}

internal sealed class FakeReservations : IReservationRepository
{
    public Dictionary<ReservationId, Reservation> Items { get; } = new();
    public Task AddAsync(Reservation root, CancellationToken ct) { Items[root.Id] = root; return Task.CompletedTask; }
    public Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
    public Task UpdateAsync(Reservation root, CancellationToken ct) { Items[root.Id] = root; return Task.CompletedTask; }

    public Task<IReadOnlyList<Reservation>> FindActiveOverlappingAsync(RoomId roomId, Hotel.Domain.Common.DateRange stay, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Reservation>>(Items.Values
            .Where(r => r.RoomId == roomId
                        && (r.Status is ReservationStatus.Confirmed or ReservationStatus.CheckedIn)
                        && r.Stay.Overlaps(stay))
            .ToList());
}
