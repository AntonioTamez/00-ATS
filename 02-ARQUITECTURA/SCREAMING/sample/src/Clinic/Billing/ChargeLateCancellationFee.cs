using Clinic.Appointments;

namespace Clinic.Billing;

/// <summary>Use case: a patient cancelled too late, so the clinic issues a fee. Billing plugs into what Appointments asked for.</summary>
public sealed class ChargeLateCancellationFee(InvoiceBook invoices, TimeProvider clock) : IFeeCharger
{
    public const decimal Fee = 25.00m;

    public void ChargeLateCancellation(Guid patientId, Guid appointmentId) =>
        invoices.Add(new Invoice(Guid.NewGuid(), patientId, $"Late cancellation of appointment {appointmentId}", Fee, clock.GetUtcNow()));
}
