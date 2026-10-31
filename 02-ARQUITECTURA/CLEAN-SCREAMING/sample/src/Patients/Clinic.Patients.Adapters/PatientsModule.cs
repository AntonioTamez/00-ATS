using Clinic.Patients.Application;
using Clinic.Patients.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Patients.Adapters;

/// <summary>OUTBOUND adapter: implements the repository port with an in-memory list.</summary>
public sealed class InMemoryPatientRepository : IPatientRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Patient> _patients = [];

    public Patient? Find(Guid id)
    {
        lock (_gate) return _patients.GetValueOrDefault(id);
    }

    public bool EmailTaken(string email)
    {
        lock (_gate) return _patients.Values.Any(p => string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public void Add(Patient patient)
    {
        lock (_gate) _patients[patient.Id] = patient;
    }
}

public sealed record RegisterPatientRequest(string? Name, string? Email);

/// <summary>INBOUND adapter + wiring for this capability. The Host calls these two methods and nothing else.</summary>
public static class PatientsModule
{
    public static IServiceCollection AddPatients(this IServiceCollection services)
    {
        services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
        services.AddScoped<RegisterPatient>();
        services.AddScoped<GetPatient>();
        return services;
    }

    public static IEndpointRouteBuilder MapPatients(this IEndpointRouteBuilder app)
    {
        app.MapPost("/patients", (RegisterPatientRequest request, RegisterPatient register) =>
        {
            var patient = register.Execute(request.Name, request.Email);
            return Results.Created($"/patients/{patient.Id}", patient);
        });

        app.MapGet("/patients/{id:guid}", (Guid id, GetPatient get) => Results.Ok(get.Execute(id)));
        return app;
    }
}
