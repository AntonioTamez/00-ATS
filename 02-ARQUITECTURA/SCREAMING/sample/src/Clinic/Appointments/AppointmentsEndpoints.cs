using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Appointments;

public sealed record BookAppointmentRequest(Guid PatientId, string? Doctor, DateTimeOffset Start);

public static class AppointmentsSetup
{
    public static IServiceCollection AddAppointments(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AppointmentBook>();
        services.AddScoped<BookAppointment>();
        services.AddScoped<CancelAppointment>();
        services.AddScoped<ListAppointments>();
        return services;
    }

    public static IEndpointRouteBuilder MapAppointments(this IEndpointRouteBuilder app)
    {
        app.MapPost("/appointments", (BookAppointmentRequest request, BookAppointment book) =>
        {
            var appointment = book.Execute(request.PatientId, request.Doctor, request.Start);
            return Results.Created($"/appointments/{appointment.Id}", appointment);
        });

        app.MapPost("/appointments/{id:guid}/cancel", (Guid id, CancelAppointment cancel) =>
            Results.Ok(cancel.Execute(id)));

        app.MapGet("/patients/{patientId:guid}/appointments", (Guid patientId, ListAppointments list) =>
            Results.Ok(list.Execute(patientId)));

        return app;
    }
}
