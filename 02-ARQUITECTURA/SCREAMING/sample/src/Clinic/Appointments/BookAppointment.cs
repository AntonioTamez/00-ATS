using Clinic.Patients;

namespace Clinic.Appointments;

/// <summary>Use case: a patient books a visit with a doctor.</summary>
public sealed class BookAppointment(IPatientDirectory patients, AppointmentBook book, TimeProvider clock)
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan OpeningTime = TimeSpan.FromHours(9);
    public static readonly TimeSpan ClosingTime = TimeSpan.FromHours(17);

    public Appointment Execute(Guid patientId, string? doctor, DateTimeOffset start)
    {
        if (patients.Find(patientId) is null) throw new NotFoundException($"Patient {patientId} was not found.");
        if (string.IsNullOrWhiteSpace(doctor)) throw new RuleViolationException("A doctor is required.");

        var utc = start.UtcDateTime;
        if (start <= clock.GetUtcNow()) throw new RuleViolationException("An appointment must be in the future.");
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) throw new RuleViolationException("The clinic is closed at weekends.");
        if (utc.TimeOfDay.Ticks % SlotLength.Ticks != 0) throw new RuleViolationException("Appointments start on the hour or the half hour.");
        if (utc.TimeOfDay < OpeningTime || utc.TimeOfDay + SlotLength > ClosingTime)
            throw new RuleViolationException("The clinic is open from 09:00 to 17:00 (UTC); the last appointment starts at 16:30.");

        var appointment = new Appointment(Guid.NewGuid(), patientId, doctor.Trim(), start.ToUniversalTime(), AppointmentStatus.Booked);
        if (!book.TryAdd(appointment)) throw new RuleViolationException($"{appointment.Doctor} already has an appointment at that time.");
        return appointment;
    }
}
