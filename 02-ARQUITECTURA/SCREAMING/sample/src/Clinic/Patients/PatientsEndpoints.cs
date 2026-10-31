namespace Clinic.Patients;

public sealed record RegisterPatientRequest(string? Name, string? Email);

// Framework code lives in *Endpoints.cs and *Setup.cs files: the use cases never see HTTP.
public static class PatientsSetup
{
    public static IServiceCollection AddPatients(this IServiceCollection services)
    {
        services.AddSingleton<PatientRegistry>();
        services.AddSingleton<IPatientDirectory>(sp => sp.GetRequiredService<PatientRegistry>());
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
