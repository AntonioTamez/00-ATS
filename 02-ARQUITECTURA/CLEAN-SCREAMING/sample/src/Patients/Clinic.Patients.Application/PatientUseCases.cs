using Clinic.Patients.Domain;
using Clinic.SharedKernel;

namespace Clinic.Patients.Application;

/// <summary>What crosses the boundary: a simple data structure, never the entity itself.</summary>
public sealed record PatientDto(Guid Id, string Name, string Email);

/// <summary>A PORT: what the use cases need from storage. The use cases define it; an adapter in the outer circle implements it.</summary>
public interface IPatientRepository
{
    Patient? Find(Guid id);
    bool EmailTaken(string email);
    void Add(Patient patient);
}

/// <summary>Use case: a person becomes a patient of the clinic.</summary>
public sealed class RegisterPatient(IPatientRepository patients)
{
    public PatientDto Execute(string? name, string? email)
    {
        var patient = Patient.Register(name, email);                       // the entity's rules
        if (patients.EmailTaken(patient.Email))                            // an application rule: it needs the repository
            throw new RuleViolationException("That email already belongs to a patient.");

        patients.Add(patient);
        return ToDto(patient);
    }

    internal static PatientDto ToDto(Patient p) => new(p.Id, p.Name, p.Email);
}

/// <summary>Use case: look up a patient. <see cref="Find"/> is what OTHER capabilities call through their own adapters.</summary>
public sealed class GetPatient(IPatientRepository patients)
{
    public PatientDto Execute(Guid patientId) =>
        Find(patientId) ?? throw new NotFoundException($"Patient {patientId} was not found.");

    public PatientDto? Find(Guid patientId) =>
        patients.Find(patientId) is { } p ? RegisterPatient.ToDto(p) : null;
}
