namespace Clinic.Patients;

public sealed record Patient(Guid Id, string Name, string Email);

/// <summary>What the other parts of the clinic are allowed to ask the Patients side.</summary>
public interface IPatientDirectory
{
    Patient? Find(Guid patientId);
}

/// <summary>The clinic's patient records (in memory).</summary>
public sealed class PatientRegistry : IPatientDirectory
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Patient> _patients = [];

    public Patient? Find(Guid patientId)
    {
        lock (_gate) return _patients.GetValueOrDefault(patientId);
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
