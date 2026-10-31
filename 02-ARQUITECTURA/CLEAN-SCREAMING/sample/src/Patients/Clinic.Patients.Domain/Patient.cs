using Clinic.SharedKernel;

namespace Clinic.Patients.Domain;

/// <summary>A person who is treated at the clinic. The rules that are true regardless of any screen or database live here.</summary>
public sealed class Patient
{
    private Patient() { }

    public Guid Id { get; private init; }
    public string Name { get; private init; } = "";
    public string Email { get; private init; } = "";

    public static Patient Register(string? name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new RuleViolationException("The patient's name is required.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new RuleViolationException("A valid email is required.");

        return new Patient { Id = Guid.NewGuid(), Name = name.Trim(), Email = email.Trim() };
    }
}
