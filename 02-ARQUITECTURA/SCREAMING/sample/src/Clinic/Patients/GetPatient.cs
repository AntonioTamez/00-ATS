namespace Clinic.Patients;

/// <summary>Use case: look up a patient.</summary>
public sealed class GetPatient(PatientRegistry registry)
{
    public Patient Execute(Guid patientId) =>
        registry.Find(patientId) ?? throw new NotFoundException($"Patient {patientId} was not found.");
}
