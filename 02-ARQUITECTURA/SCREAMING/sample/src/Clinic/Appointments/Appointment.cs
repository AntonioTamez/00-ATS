namespace Clinic.Appointments;

public enum AppointmentStatus { Booked, Cancelled }

public sealed record Appointment(Guid Id, Guid PatientId, string Doctor, DateTimeOffset Start, AppointmentStatus Status);

/// <summary>What Appointments needs from whoever handles the money. It does not know Billing exists.</summary>
public interface IFeeCharger
{
    void ChargeLateCancellation(Guid patientId, Guid appointmentId);
}

/// <summary>The clinic's agenda (in memory).</summary>
public sealed class AppointmentBook
{
    private readonly object _gate = new();
    private readonly List<Appointment> _appointments = [];

    public Appointment? Find(Guid id)
    {
        lock (_gate) return _appointments.FirstOrDefault(a => a.Id == id);
    }

    public IReadOnlyList<Appointment> ForPatient(Guid patientId)
    {
        lock (_gate) return _appointments.Where(a => a.PatientId == patientId).OrderBy(a => a.Start).ToList();
    }

    public bool DoctorIsBusy(string doctor, DateTimeOffset start)
    {
        lock (_gate)
            return _appointments.Any(a => a.Status == AppointmentStatus.Booked && a.Start == start &&
                                          string.Equals(a.Doctor, doctor, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds the appointment unless the doctor became busy in the meantime (check and add are one step).</summary>
    public bool TryAdd(Appointment appointment)
    {
        lock (_gate)
        {
            if (DoctorIsBusy(appointment.Doctor, appointment.Start)) return false;
            _appointments.Add(appointment);
            return true;
        }
    }

    public void Replace(Appointment appointment)
    {
        lock (_gate)
        {
            var index = _appointments.FindIndex(a => a.Id == appointment.Id);
            if (index >= 0) _appointments[index] = appointment;
        }
    }
}
