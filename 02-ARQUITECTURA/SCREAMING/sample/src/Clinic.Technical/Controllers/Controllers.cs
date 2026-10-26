using Clinic.Technical.Services;

namespace Clinic.Technical.Controllers;

public static class PatientController
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/patients", (RegisterRequest r, PatientService s) => Results.Ok(s.Register(r.Name, r.Email)));

    public sealed record RegisterRequest(string Name, string Email);
}

public static class AppointmentController
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/appointments", (BookRequest r, AppointmentService s) =>
            s.Book(r.PatientId, r.Doctor, r.Start) is { } a ? Results.Ok(a) : Results.UnprocessableEntity());

    public sealed record BookRequest(Guid PatientId, string Doctor, DateTimeOffset Start);
}
