namespace Clinic.Technical.Models;

public sealed record Patient(Guid Id, string Name, string Email);

public sealed record Appointment(Guid Id, Guid PatientId, string Doctor, DateTimeOffset Start);
