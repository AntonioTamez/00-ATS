using Clinic.Patients;

namespace Clinic.Appointments;

/// <summary>Use case: see a patient's visits, in date order.</summary>
public sealed class ListAppointments(IPatientDirectory patients, AppointmentBook book)
{
    public IReadOnlyList<Appointment> Execute(Guid patientId) =>
        patients.Find(patientId) is null
            ? throw new NotFoundException($"Patient {patientId} was not found.")
            : book.ForPatient(patientId);
}
