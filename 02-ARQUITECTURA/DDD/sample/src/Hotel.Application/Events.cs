using Hotel.Domain.Common;
using Hotel.Domain.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace Hotel.Application;

/// <summary>Finds and runs every handler registered for an event, after the aggregate has been saved.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services)
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

            foreach (var handler in services.GetServices(handlerType))
                await (Task)handle.Invoke(handler, [domainEvent, ct])!;
        }
    }
}

public sealed class NotifyGuestWhenReservationConfirmed(INotifier notifier) : IDomainEventHandler<ReservationConfirmed>
{
    public Task HandleAsync(ReservationConfirmed e, CancellationToken ct) =>
        notifier.SendAsync(e.GuestEmail, "Reservation confirmed",
            $"Your stay from {e.Stay.CheckIn:yyyy-MM-dd} to {e.Stay.CheckOut:yyyy-MM-dd} is confirmed. Total: {e.Total.Amount:0.00} {e.Total.Currency}.", ct);
}

public sealed class NotifyGuestWhenReservationCancelled(INotifier notifier) : IDomainEventHandler<ReservationCancelled>
{
    public Task HandleAsync(ReservationCancelled e, CancellationToken ct) =>
        notifier.SendAsync(e.GuestEmail, "Reservation cancelled", "Your reservation has been cancelled.", ct);
}
