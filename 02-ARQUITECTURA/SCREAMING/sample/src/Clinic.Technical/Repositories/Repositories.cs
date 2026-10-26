using Clinic.Technical.Models;

namespace Clinic.Technical.Repositories;

public sealed class PatientRepository
{
    private readonly List<Patient> _items = [];
    public void Add(Patient p) => _items.Add(p);
    public Patient? Find(Guid id) => _items.FirstOrDefault(p => p.Id == id);
}

public sealed class AppointmentRepository
{
    private readonly List<Appointment> _items = [];
    public void Add(Appointment a) => _items.Add(a);
    public bool Busy(string doctor, DateTimeOffset start) => _items.Any(a => a.Doctor == doctor && a.Start == start);
}
