using Clinic.SharedKernel;

namespace Clinic.Billing.Domain;

/// <summary>An amount the clinic charges a patient, and why.</summary>
public sealed class Invoice
{
    public const decimal LateCancellationFee = 25.00m;

    private Invoice() { }

    public Guid Id { get; private init; }
    public Guid PatientId { get; private init; }
    public string Reason { get; private init; } = "";
    public decimal Amount { get; private init; }
    public DateTimeOffset IssuedAt { get; private init; }

    public static Invoice Issue(Guid patientId, string reason, decimal amount, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new RuleViolationException("An invoice needs a reason.");
        if (amount <= 0) throw new RuleViolationException("An invoice amount must be positive.");

        return new Invoice { Id = Guid.NewGuid(), PatientId = patientId, Reason = reason.Trim(), Amount = amount, IssuedAt = now };
    }

    public static Invoice ForLateCancellation(Guid patientId, Guid appointmentId, DateTimeOffset now) =>
        Issue(patientId, $"Late cancellation of appointment {appointmentId}", LateCancellationFee, now);
}
