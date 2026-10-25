using System.Collections.Concurrent;
using Hotel.Application;
using Hotel.Domain;
using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Hotel.Domain.Rooms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hotel.Infrastructure;

internal sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<RoomId, Room> _store = new();

    public Task AddAsync(Room root, CancellationToken ct) { _store[root.Id] = root; return Task.CompletedTask; }
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken ct) => Task.FromResult(_store.GetValueOrDefault(id));
    public Task UpdateAsync(Room root, CancellationToken ct) { _store[root.Id] = root; return Task.CompletedTask; }
    public Task<IReadOnlyList<Room>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Room>>(_store.Values.OrderBy(r => r.Number).ToList());
}

internal sealed class InMemoryReservationRepository : IReservationRepository
{
    private readonly ConcurrentDictionary<ReservationId, Reservation> _store = new();

    public Task AddAsync(Reservation root, CancellationToken ct) { _store[root.Id] = root; return Task.CompletedTask; }
    public Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken ct) => Task.FromResult(_store.GetValueOrDefault(id));
    public Task UpdateAsync(Reservation root, CancellationToken ct) { _store[root.Id] = root; return Task.CompletedTask; }

    public Task<IReadOnlyList<Reservation>> FindActiveOverlappingAsync(RoomId roomId, DateRange stay, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Reservation>>(_store.Values
            .Where(r => r.RoomId == roomId
                        && (r.Status is ReservationStatus.Confirmed or ReservationStatus.CheckedIn)
                        && r.Stay.Overlaps(stay))
            .ToList());
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Driven adapter for notifications. A real one would send e-mail; this one logs.</summary>
internal sealed class LogNotifier(ILogger<LogNotifier> logger) : INotifier
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("Notification to {To}: {Subject} - {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRoomRepository, InMemoryRoomRepository>();
        services.AddSingleton<IReservationRepository, InMemoryReservationRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<INotifier, LogNotifier>();
        return services;
    }
}
