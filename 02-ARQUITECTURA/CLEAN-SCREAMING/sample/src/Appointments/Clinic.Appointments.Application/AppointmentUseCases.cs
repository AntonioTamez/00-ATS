using Clinic.Appointments.Domain;
using Clinic.SharedKernel;

namespace Clinic.Appointments.Application;

public sealed record AppointmentDto(Guid Id, Guid PatientId, string Doctor, DateTimeOffset Start, string Status);

public sealed record CancellationDto(AppointmentDto Appointment, bool LateCancellation);

// ----- PORTS: what Appointments needs. It defines them; adapters in the outer circle provide them. -----

public interface IAppointmentRepository
{
    Appointment? Find(Guid id);
    IReadOnlyList<Appointment> ForPatient(Guid patientId);

    /// <summary>Adds the appointment unless the doctor is already booked at that time (check and add are one step).</summary>
    bool TryAdd(Appointment appointment);
}

/// <summary>"Does this patient exist?" Answered by whoever knows patients; Appointments does not know who that is.</summary>
public interface IPatientLookup
{
    bool Exists(Guid patientId);
}

/// <summary>"Charge this patient a late-cancellation fee." Answered by whoever handles the money.</summary>
public interface IFeeCharger
{
    void ChargeLateCancellation(Guid patientId, Guid appointmentId);
}

internal static class AppointmentMapping
{
    public static AppointmentDto ToDto(Appointment a) => new(a.Id, a.PatientId, a.Doctor, a.Start, a.Status.ToString());
}

/// <summary>Use case: a patient books a visit with a doctor.</summary>
public sealed class BookAppointment(IPatientLookup patients, IAppointmentRepository appointments, TimeProvider clock)
{
    public AppointmentDto Execute(Guid patientId, string? doctor, DateTimeOffset start)
    {
        if (!patients.Exists(patientId)) throw new NotFoundException($"Patient {patientId} was not found.");

        var appointment = Appointment.Book(patientId, doctor, start, clock.GetUtcNow());      // the entity's rules
        if (!appointments.TryAdd(appointment))                                                // an application rule: availability
            throw new RuleViolationException($"{appointment.Doctor} already has an appointment at that time.");

        return AppointmentMapping.ToDto(appointment);
    }
}

/// <summary>Use case: a patient cancels a visit. Cancelling too late triggers a fee.</summary>
public sealed class CancelAppointment(IAppointmentRepository appointments, IFeeCharger fees, TimeProvider clock)
{
    public CancellationDto Execute(Guid appointmentId)
    {
        var appointment = appointments.Find(appointmentId) ?? throw new NotFoundException($"Appointment {appointmentId} was not found.");

        var late = appointment.Cancel(clock.GetUtcNow());                                      // the entity decides what "late" means
        if (late) fees.ChargeLateCancellation(appointment.PatientId, appointment.Id);

        return new CancellationDto(AppointmentMapping.ToDto(appointment), late);
    }
}

/// <summary>Use case: see a patient's visits, in date order.</summary>
public sealed class ListAppointments(IPatientLookup patients, IAppointmentRepository appointments)
{
    public IReadOnlyList<AppointmentDto> Execute(Guid patientId) =>
        !patients.Exists(patientId)
            ? throw new NotFoundException($"Patient {patientId} was not found.")
            : appointments.ForPatient(patientId).Select(AppointmentMapping.ToDto).ToList();
}
