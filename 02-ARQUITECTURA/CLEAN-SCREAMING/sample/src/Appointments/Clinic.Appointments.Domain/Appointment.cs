using Clinic.SharedKernel;

namespace Clinic.Appointments.Domain;

public enum AppointmentStatus { Booked, Cancelled }

/// <summary>
/// A visit. The clinic's schedule and its cancellation notice are ENTERPRISE rules: they hold whatever the
/// screen or the database look like, so they live in the innermost circle. Time is passed in; the entity never reads a clock.
/// </summary>
public sealed class Appointment
{
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan OpeningTime = TimeSpan.FromHours(9);
    public static readonly TimeSpan ClosingTime = TimeSpan.FromHours(17);
    public static readonly TimeSpan FreeCancellationNotice = TimeSpan.FromHours(24);

    private Appointment() { }

    public Guid Id { get; private init; }
    public Guid PatientId { get; private init; }
    public string Doctor { get; private init; } = "";
    public DateTimeOffset Start { get; private init; }
    public AppointmentStatus Status { get; private set; }

    public static Appointment Book(Guid patientId, string? doctor, DateTimeOffset start, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(doctor)) throw new RuleViolationException("A doctor is required.");

        var utc = start.UtcDateTime;
        if (start <= now) throw new RuleViolationException("An appointment must be in the future.");
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) throw new RuleViolationException("The clinic is closed at weekends.");
        if (utc.TimeOfDay.Ticks % SlotLength.Ticks != 0) throw new RuleViolationException("Appointments start on the hour or the half hour.");
        if (utc.TimeOfDay < OpeningTime || utc.TimeOfDay + SlotLength > ClosingTime)
            throw new RuleViolationException("The clinic is open from 09:00 to 17:00 (UTC); the last appointment starts at 16:30.");

        return new Appointment
        {
            Id = Guid.NewGuid(), PatientId = patientId, Doctor = doctor.Trim(),
            Start = start.ToUniversalTime(), Status = AppointmentStatus.Booked,
        };
    }

    /// <returns>true when the cancellation is late (less than 24 hours before the visit).</returns>
    public bool Cancel(DateTimeOffset now)
    {
        if (Status == AppointmentStatus.Cancelled) throw new RuleViolationException("That appointment is already cancelled.");

        Status = AppointmentStatus.Cancelled;
        return Start - now < FreeCancellationNotice;
    }
}
