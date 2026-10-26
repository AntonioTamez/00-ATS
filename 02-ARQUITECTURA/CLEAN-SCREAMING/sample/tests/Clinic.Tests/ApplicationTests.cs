using Clinic.Appointments.Application;
using Clinic.Appointments.Domain;
using Clinic.Billing.Application;
using Clinic.Billing.Domain;
using Clinic.Patients.Application;
using Clinic.Patients.Domain;
using Clinic.SharedKernel;

namespace Clinic.Tests;

internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => start;
}

// Hand-written fakes for the PORTS. Because the use cases only know the interfaces, these few lines replace
// the whole database, the whole web server and the other capabilities.

internal sealed class FakePatientRepository : IPatientRepository
{
    public List<Patient> Items { get; } = [];
    public Patient? Find(Guid id) => Items.FirstOrDefault(p => p.Id == id);
    public bool EmailTaken(string email) => Items.Any(p => string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase));
    public void Add(Patient patient) => Items.Add(patient);
}

internal sealed class FakeAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Items { get; } = [];
    public Appointment? Find(Guid id) => Items.FirstOrDefault(a => a.Id == id);
    public IReadOnlyList<Appointment> ForPatient(Guid patientId) => Items.Where(a => a.PatientId == patientId).OrderBy(a => a.Start).ToList();

    public bool TryAdd(Appointment appointment)
    {
        if (Items.Any(a => a.Status == AppointmentStatus.Booked && a.Start == appointment.Start && a.Doctor == appointment.Doctor)) return false;
        Items.Add(appointment);
        return true;
    }
}

internal sealed class FakePatientLookup(params Guid[] known) : IPatientLookup
{
    public bool Exists(Guid patientId) => known.Contains(patientId);
}

internal sealed class SpyFeeCharger : IFeeCharger
{
    public List<(Guid Patient, Guid Appointment)> Charges { get; } = [];
    public void ChargeLateCancellation(Guid patientId, Guid appointmentId) => Charges.Add((patientId, appointmentId));
}

internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Items { get; } = [];
    public void Add(Invoice invoice) => Items.Add(invoice);
    public IReadOnlyList<Invoice> ForPatient(Guid patientId) => Items.Where(i => i.PatientId == patientId).ToList();
}

/// <summary>
/// The use cases (second circle) with FAKE ports. No web server, no real storage, no other capability:
/// each use case is tested alone, and the fakes show exactly what it asks of the outside world.
/// </summary>
public class ApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int day, int hour) => new(2026, 3, day, hour, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTime(Now);
    private readonly Guid _ana = Guid.NewGuid();

    // ---------- Patients ----------

    [Fact]
    public void RegisteringAPatient_StoresTheEntity_AndReturnsAPlainDto()
    {
        var repository = new FakePatientRepository();

        var dto = new RegisterPatient(repository).Execute("Ana", "ana@x.com");

        Assert.Equal(dto.Id, Assert.Single(repository.Items).Id);
        Assert.Equal(("Ana", "ana@x.com"), (dto.Name, dto.Email));
    }

    [Fact]
    public void ARepeatedEmail_IsRefused_ByTheUseCase_NotByTheEntity()
    {
        var repository = new FakePatientRepository();
        var register = new RegisterPatient(repository);
        register.Execute("Ana", "ana@x.com");

        var ex = Assert.Throws<RuleViolationException>(() => register.Execute("Other", "ANA@x.com"));

        Assert.Contains("already belongs", ex.Message);
        Assert.Single(repository.Items);
    }

    [Fact]
    public void ABrokenEntityRule_StopsBeforeAnythingIsStored()
    {
        var repository = new FakePatientRepository();

        Assert.Throws<RuleViolationException>(() => new RegisterPatient(repository).Execute("", "ana@x.com"));

        Assert.Empty(repository.Items);
    }

    [Fact]
    public void GettingAPatient_FindsIt_OrIsNotFound()
    {
        var repository = new FakePatientRepository();
        var dto = new RegisterPatient(repository).Execute("Ana", "ana@x.com");
        var get = new GetPatient(repository);

        Assert.Equal(dto, get.Execute(dto.Id));
        Assert.Null(get.Find(Guid.NewGuid()));
        Assert.Throws<NotFoundException>(() => get.Execute(Guid.NewGuid()));
    }

    // ---------- Appointments ----------

    [Fact]
    public void Booking_AsksThePatientsPort_ThenStoresTheAppointment()
    {
        var repository = new FakeAppointmentRepository();

        var dto = new BookAppointment(new FakePatientLookup(_ana), repository, _clock).Execute(_ana, "Dr. Who", At(3, 10));

        Assert.Equal(dto.Id, Assert.Single(repository.Items).Id);
        Assert.Equal("Booked", dto.Status);
    }

    [Fact]
    public void Booking_ForAnUnknownPatient_IsNotFound_AndStoresNothing()
    {
        var repository = new FakeAppointmentRepository();

        Assert.Throws<NotFoundException>(() => new BookAppointment(new FakePatientLookup(), repository, _clock).Execute(_ana, "Dr. Who", At(3, 10)));

        Assert.Empty(repository.Items);
    }

    [Fact]
    public void ADoubleBooking_IsRefused_ByTheRepositorysAvailabilityCheck()
    {
        var repository = new FakeAppointmentRepository();
        var book = new BookAppointment(new FakePatientLookup(_ana), repository, _clock);
        book.Execute(_ana, "Dr. Who", At(3, 10));

        var ex = Assert.Throws<RuleViolationException>(() => book.Execute(_ana, "Dr. Who", At(3, 10)));

        Assert.Contains("already has an appointment", ex.Message);
    }

    [Fact]
    public void AnEntityRule_StillApplies_ThroughTheUseCase() =>
        Assert.Throws<RuleViolationException>(() =>
            new BookAppointment(new FakePatientLookup(_ana), new FakeAppointmentRepository(), _clock).Execute(_ana, "Dr. Who", At(7, 10)));   // Saturday

    [Fact]
    public void ALateCancellation_CallsTheFeeChargerPort_Once()
    {
        var repository = new FakeAppointmentRepository();
        var book = new BookAppointment(new FakePatientLookup(_ana), repository, _clock).Execute(_ana, "Dr. Who", At(2, 15));
        var fees = new SpyFeeCharger();

        var result = new CancelAppointment(repository, fees, _clock).Execute(book.Id);

        Assert.True(result.LateCancellation);
        Assert.Equal((_ana, book.Id), Assert.Single(fees.Charges));
    }

    [Fact]
    public void AnEarlyCancellation_DoesNotTouchTheFeeChargerPort()
    {
        var repository = new FakeAppointmentRepository();
        var book = new BookAppointment(new FakePatientLookup(_ana), repository, _clock).Execute(_ana, "Dr. Who", At(5, 10));
        var fees = new SpyFeeCharger();

        var result = new CancelAppointment(repository, fees, _clock).Execute(book.Id);

        Assert.False(result.LateCancellation);
        Assert.Empty(fees.Charges);
    }

    [Fact]
    public void CancellingTwice_ChargesNothingTheSecondTime()
    {
        var repository = new FakeAppointmentRepository();
        var book = new BookAppointment(new FakePatientLookup(_ana), repository, _clock).Execute(_ana, "Dr. Who", At(2, 15));
        var fees = new SpyFeeCharger();
        var cancel = new CancelAppointment(repository, fees, _clock);
        cancel.Execute(book.Id);

        Assert.Throws<RuleViolationException>(() => cancel.Execute(book.Id));
        Assert.Single(fees.Charges);
    }

    [Fact]
    public void CancellingAnUnknownAppointment_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => new CancelAppointment(new FakeAppointmentRepository(), new SpyFeeCharger(), _clock).Execute(Guid.NewGuid()));

    [Fact]
    public void ListingAppointments_IsByDate_AndNeedsAKnownPatient()
    {
        var repository = new FakeAppointmentRepository();
        var book = new BookAppointment(new FakePatientLookup(_ana), repository, _clock);
        book.Execute(_ana, "Dr. Who", At(5, 10));
        book.Execute(_ana, "Dr. Who", At(3, 10));

        var list = new ListAppointments(new FakePatientLookup(_ana), repository).Execute(_ana);

        Assert.Equal([At(3, 10), At(5, 10)], list.Select(a => a.Start));
        Assert.Throws<NotFoundException>(() => new ListAppointments(new FakePatientLookup(), repository).Execute(_ana));
    }

    // ---------- Billing ----------

    [Fact]
    public void ChargingALateFee_StoresAnInvoice_AndReturnsADto()
    {
        var repository = new FakeInvoiceRepository();

        var dto = new ChargeLateCancellationFee(repository, _clock).Execute(_ana, Guid.NewGuid());

        Assert.Equal(dto.Id, Assert.Single(repository.Items).Id);
        Assert.Equal(Invoice.LateCancellationFee, dto.Amount);
    }

    [Fact]
    public void ListingInvoices_ShowsOnlyThatPatientsInvoices()
    {
        var repository = new FakeInvoiceRepository();
        var charge = new ChargeLateCancellationFee(repository, _clock);
        charge.Execute(_ana, Guid.NewGuid());
        charge.Execute(Guid.NewGuid(), Guid.NewGuid());

        Assert.Single(new ListInvoices(repository).Execute(_ana));
    }
}
