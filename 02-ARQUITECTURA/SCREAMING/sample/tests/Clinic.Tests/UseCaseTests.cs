using Clinic.Appointments;
using Clinic.Billing;
using Clinic.Patients;

namespace Clinic.Tests;

internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => start;
}

/// <summary>
/// The use cases run with NO web server, NO HTTP and NO framework: the whole clinic in a few objects.
/// That is what "the web is a detail" means in practice.
/// </summary>
public class UseCaseTests
{
    // Monday, 2 March 2026, 08:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly PatientRegistry _registry = new();
    private readonly AppointmentBook _agenda = new();
    private readonly InvoiceBook _invoices = new();
    private readonly TimeProvider _clock = new FakeTime(Now);

    private RegisterPatient Register => new(_registry);
    private BookAppointment Book => new(_registry, _agenda, _clock);
    private CancelAppointment Cancel => new(_agenda, new ChargeLateCancellationFee(_invoices, _clock), _clock);

    private Guid NewPatient(string email = "ana@x.com") => Register.Execute("Ana", email).Id;

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    // ---------- Patients ----------

    [Fact]
    public void ARegisteredPatient_CanBeFound()
    {
        var patient = Register.Execute("  Ana  ", " ana@x.com ");

        Assert.Equal(("Ana", "ana@x.com"), (patient.Name, patient.Email));
        Assert.Equal(patient, new GetPatient(_registry).Execute(patient.Id));
    }

    [Theory]
    [InlineData("", "ana@x.com")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "not-an-email")]
    public void APatientNeedsANameAndAValidEmail(string name, string email) =>
        Assert.Throws<RuleViolationException>(() => Register.Execute(name, email));

    [Fact]
    public void TwoPatients_CannotShareAnEmail()
    {
        Register.Execute("Ana", "ana@x.com");

        Assert.Throws<RuleViolationException>(() => Register.Execute("Other", "ANA@x.com"));
    }

    [Fact]
    public void AnUnknownPatient_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => new GetPatient(_registry).Execute(Guid.NewGuid()));

    // ---------- booking ----------

    [Fact]
    public void ABooking_InTheOpeningHours_Works()
    {
        var patient = NewPatient();

        var appointment = Book.Execute(patient, "Dr. Who", At(3, 10));

        Assert.Equal((patient, "Dr. Who", AppointmentStatus.Booked), (appointment.PatientId, appointment.Doctor, appointment.Status));
    }

    [Fact]
    public void TheLastSlotOfTheDay_StartsAt1630()
    {
        var patient = NewPatient();

        Book.Execute(patient, "Dr. Who", At(3, 16, 30));
        Assert.Throws<RuleViolationException>(() => Book.Execute(patient, "Dr. Who", At(3, 17)));
    }

    [Theory]
    [InlineData(3, 8, 30)]    // before opening
    [InlineData(3, 10, 15)]   // not on a slot boundary
    [InlineData(7, 10, 0)]    // Saturday
    [InlineData(8, 10, 0)]    // Sunday
    public void ABooking_OutsideTheClinicsSchedule_IsRefused(int day, int hour, int minute)
    {
        var patient = NewPatient();

        Assert.Throws<RuleViolationException>(() => Book.Execute(patient, "Dr. Who", At(day, hour, minute)));
    }

    [Fact]
    public void ABooking_InThePast_IsRefused()
    {
        var patient = NewPatient();

        Assert.Throws<RuleViolationException>(() => Book.Execute(patient, "Dr. Who", At(2, 7)));
    }

    [Fact]
    public void ADoctor_CannotBeBookedTwiceAtTheSameTime()
    {
        var ana = NewPatient("ana@x.com");
        var bob = NewPatient("bob@x.com");
        Book.Execute(ana, "Dr. Who", At(3, 10));

        var ex = Assert.Throws<RuleViolationException>(() => Book.Execute(bob, "dr. who", At(3, 10)));

        Assert.Contains("already has an appointment", ex.Message);
        Book.Execute(bob, "Dr. Strange", At(3, 10));            // another doctor, same time: fine
    }

    [Fact]
    public void ABookingForAnUnknownPatient_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => Book.Execute(Guid.NewGuid(), "Dr. Who", At(3, 10)));

    [Fact]
    public void ABookingNeedsADoctor() =>
        Assert.Throws<RuleViolationException>(() => Book.Execute(NewPatient(), " ", At(3, 10)));

    // ---------- cancelling ----------

    [Fact]
    public void CancellingWithMoreThan24HoursNotice_IsFree()
    {
        var patient = NewPatient();
        var appointment = Book.Execute(patient, "Dr. Who", At(4, 10));         // 50 hours ahead

        var result = Cancel.Execute(appointment.Id);

        Assert.False(result.LateCancellation);
        Assert.Equal(AppointmentStatus.Cancelled, result.Appointment.Status);
        Assert.Empty(_invoices.ForPatient(patient));
    }

    [Fact]
    public void CancellingWithLessThan24HoursNotice_IssuesAFee_ThroughBilling()
    {
        var patient = NewPatient();
        var appointment = Book.Execute(patient, "Dr. Who", At(2, 15));         // 7 hours ahead

        var result = Cancel.Execute(appointment.Id);

        Assert.True(result.LateCancellation);
        var invoice = Assert.Single(_invoices.ForPatient(patient));
        Assert.Equal((ChargeLateCancellationFee.Fee, patient), (invoice.Amount, invoice.PatientId));
        Assert.Contains(appointment.Id.ToString(), invoice.Reason);
    }

    [Fact]
    public void CancellingExactly24HoursBefore_IsStillFree()
    {
        var appointment = Book.Execute(NewPatient(), "Dr. Who", At(3, 10));    // Tuesday 10:00
        var mondayAt10 = new FakeTime(At(2, 10));                              // exactly 24 hours before

        var result = new CancelAppointment(_agenda, new ChargeLateCancellationFee(_invoices, mondayAt10), mondayAt10).Execute(appointment.Id);

        Assert.False(result.LateCancellation);
    }

    [Fact]
    public void AnAppointment_CannotBeCancelledTwice_NorChargedTwice()
    {
        var patient = NewPatient();
        var appointment = Book.Execute(patient, "Dr. Who", At(2, 15));
        Cancel.Execute(appointment.Id);

        Assert.Throws<RuleViolationException>(() => Cancel.Execute(appointment.Id));
        Assert.Single(_invoices.ForPatient(patient));
    }

    [Fact]
    public void ACancelledSlot_CanBeBookedAgain()
    {
        var patient = NewPatient();
        var first = Book.Execute(patient, "Dr. Who", At(4, 10));
        Cancel.Execute(first.Id);

        Book.Execute(patient, "Dr. Who", At(4, 10));
    }

    [Fact]
    public void CancellingAnUnknownAppointment_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => Cancel.Execute(Guid.NewGuid()));

    // ---------- listing ----------

    [Fact]
    public void APatientsAppointments_AreListedByDate()
    {
        var patient = NewPatient();
        Book.Execute(patient, "Dr. Who", At(5, 10));
        Book.Execute(patient, "Dr. Who", At(3, 10));

        var list = new ListAppointments(_registry, _agenda).Execute(patient);

        Assert.Equal([At(3, 10), At(5, 10)], list.Select(a => a.Start));
    }

    [Fact]
    public void ListingForAnUnknownPatient_IsNotFound() =>
        Assert.Throws<NotFoundException>(() => new ListAppointments(_registry, _agenda).Execute(Guid.NewGuid()));
}
