using Clinic.Technical.Models;
using Clinic.Technical.Repositories;

namespace Clinic.Technical.Services;

public sealed class PatientService(PatientRepository repository)
{
    public Patient Register(string name, string email)
    {
        var patient = new Patient(Guid.NewGuid(), name, email);
        repository.Add(patient);
        return patient;
    }
}

public sealed class AppointmentService(PatientRepository patients, AppointmentRepository appointments)
{
    public Appointment? Book(Guid patientId, string doctor, DateTimeOffset start)
    {
        if (patients.Find(patientId) is null || appointments.Busy(doctor, start)) return null;
        var appointment = new Appointment(Guid.NewGuid(), patientId, doctor, start);
        appointments.Add(appointment);
        return appointment;
    }
}
