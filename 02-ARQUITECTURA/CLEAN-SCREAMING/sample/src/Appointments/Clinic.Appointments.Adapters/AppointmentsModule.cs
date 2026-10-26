using Clinic.Appointments.Application;
using Clinic.Appointments.Domain;
using Clinic.Patients.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Appointments.Adapters;

/// <summary>OUTBOUND adapter: the agenda, in memory.</summary>
public sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
    private readonly object _gate = new();
    private readonly List<Appointment> _appointments = [];

    public Appointment? Find(Guid id)
    {
        lock (_gate) return _appointments.FirstOrDefault(a => a.Id == id);
    }

    public IReadOnlyList<Appointment> ForPatient(Guid patientId)
    {
        lock (_gate) return _appointments.Where(a => a.PatientId == patientId).OrderBy(a => a.Start).ToList();
    }

    public bool TryAdd(Appointment appointment)
    {
        lock (_gate)
        {
            var busy = _appointments.Any(a => a.Status == AppointmentStatus.Booked && a.Start == appointment.Start &&
                                              string.Equals(a.Doctor, appointment.Doctor, StringComparison.OrdinalIgnoreCase));
            if (busy) return false;
            _appointments.Add(appointment);
            return true;
        }
    }
}

/// <summary>
/// OUTBOUND adapter to ANOTHER capability. It answers Appointments' question ("does this patient exist?") by calling
/// Patients' use case. This is the seam between the two capabilities: neither inner circle knows the other exists.
/// </summary>
public sealed class PatientLookupAdapter(GetPatient getPatient) : IPatientLookup
{
    public bool Exists(Guid patientId) => getPatient.Find(patientId) is not null;
}

public sealed record BookAppointmentRequest(Guid PatientId, string? Doctor, DateTimeOffset Start);

public static class AppointmentsModule
{
    public static IServiceCollection AddAppointments(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAppointmentRepository, InMemoryAppointmentRepository>();
        services.AddScoped<IPatientLookup, PatientLookupAdapter>();
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

        app.MapPost("/appointments/{id:guid}/cancel", (Guid id, CancelAppointment cancel) => Results.Ok(cancel.Execute(id)));

        app.MapGet("/patients/{patientId:guid}/appointments", (Guid patientId, ListAppointments list) =>
            Results.Ok(list.Execute(patientId)));

        return app;
    }
}
