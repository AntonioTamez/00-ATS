namespace Clinic.Patients;

/// <summary>Use case: a person becomes a patient of the clinic. No web, no database: just the rules.</summary>
public sealed class RegisterPatient(PatientRegistry registry)
{
    public Patient Execute(string? name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new RuleViolationException("The patient's name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new RuleViolationException("A valid email is required.");
        if (registry.EmailTaken(email.Trim())) throw new RuleViolationException("That email already belongs to a patient.");

        var patient = new Patient(Guid.NewGuid(), name.Trim(), email.Trim());
        registry.Add(patient);
        return patient;
    }
}
