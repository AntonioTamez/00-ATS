namespace Clinic.Appointments;

public sealed record CancellationResult(Appointment Appointment, bool LateCancellation);

/// <summary>Use case: a patient cancels a visit. Cancelling less than 24 hours before costs a fee.</summary>
public sealed class CancelAppointment(AppointmentBook book, IFeeCharger fees, TimeProvider clock)
{
    public static readonly TimeSpan FreeCancellationNotice = TimeSpan.FromHours(24);

    public CancellationResult Execute(Guid appointmentId)
    {
        var appointment = book.Find(appointmentId) ?? throw new NotFoundException($"Appointment {appointmentId} was not found.");
        if (appointment.Status == AppointmentStatus.Cancelled) throw new RuleViolationException("That appointment is already cancelled.");

        var late = appointment.Start - clock.GetUtcNow() < FreeCancellationNotice;
        var cancelled = appointment with { Status = AppointmentStatus.Cancelled };
        book.Replace(cancelled);

        if (late) fees.ChargeLateCancellation(appointment.PatientId, appointment.Id);
        return new CancellationResult(cancelled, late);
    }
}
