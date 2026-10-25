using Hotel.Domain.Reservations;
using Microsoft.Extensions.DependencyInjection;

namespace Hotel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<BookingService>();          // domain service
        services.AddScoped<DomainEventDispatcher>();

        services.AddScoped<IDomainEventHandler<ReservationConfirmed>, NotifyGuestWhenReservationConfirmed>();
        services.AddScoped<IDomainEventHandler<ReservationCancelled>, NotifyGuestWhenReservationCancelled>();

        services.AddScoped<RegisterRoom>();
        services.AddScoped<BookRoom>();
        services.AddScoped<CancelReservation>();
        services.AddScoped<CheckInGuest>();
        services.AddScoped<CheckOutGuest>();
        services.AddScoped<AddCharge>();
        services.AddScoped<GetReservation>();
        return services;
    }
}
