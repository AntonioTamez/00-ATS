using Clinic.Appointments.Domain;
using Clinic.Billing.Domain;
using Clinic.Patients.Domain;
using Clinic.SharedKernel;

namespace Clinic.Tests;

/// <summary>The entities alone (innermost circle): no mocks, no ports, no framework. Plain objects and plain rules.</summary>
public class DomainTests
{
    // Monday, 2 March 2026, 08:00 UTC
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 3, day, hour, minute, 0, TimeSpan.Zero);
    private static readonly Guid Ana = Guid.NewGuid();

    // ---------- Patient ----------

    [Fact]
    public void ARegisteredPatient_IsTrimmed_AndHasAnId()
    {
        var patient = Patient.Register("  Ana ", " ana@x.com ");

        Assert.Equal(("Ana", "ana@x.com"), (patient.Name, patient.Email));
        Assert.NotEqual(Guid.Empty, patient.Id);
    }

    [Theory]
    [InlineData("", "ana@x.com")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "not-an-email")]
    public void APatientNeedsANameAndAValidEmail(string name, string email) =>
        Assert.Throws<RuleViolationException>(() => Patient.Register(name, email));

    // ---------- Appointment ----------

    [Fact]
    public void ABookedAppointment_StartsBooked_InUtc()
    {
        var appointment = Appointment.Book(Ana, "  Dr. Who ", At(3, 10), Now);

        Assert.Equal(("Dr. Who", AppointmentStatus.Booked, At(3, 10)), (appointment.Doctor, appointment.Status, appointment.Start));
    }

    [Theory]
    [InlineData(3, 8, 30)]    // before opening
    [InlineData(3, 17, 0)]    // the last slot starts at 16:30
    [InlineData(3, 10, 15)]   // not on a slot boundary
    [InlineData(7, 10, 0)]    // Saturday
    [InlineData(8, 10, 0)]    // Sunday
    public void BookingOutsideTheClinicsSchedule_IsRefused(int day, int hour, int minute) =>
        Assert.Throws<RuleViolationException>(() => Appointment.Book(Ana, "Dr. Who", At(day, hour, minute), Now));

    [Fact]
    public void TheLastSlot_StartsAt1630() =>
        Assert.Equal(At(3, 16, 30), Appointment.Book(Ana, "Dr. Who", At(3, 16, 30), Now).Start);

    [Fact]
    public void BookingInThePast_OrNow_IsRefused()
    {
        Assert.Throws<RuleViolationException>(() => Appointment.Book(Ana, "Dr. Who", At(2, 7), Now));
        Assert.Throws<RuleViolationException>(() => Appointment.Book(Ana, "Dr. Who", Now, Now));
    }

    [Fact]
    public void BookingNeedsADoctor() =>
        Assert.Throws<RuleViolationException>(() => Appointment.Book(Ana, " ", At(3, 10), Now));

    [Fact]
    public void CancellingWithMoreThan24HoursNotice_IsNotLate()
    {
        var appointment = Appointment.Book(Ana, "Dr. Who", At(4, 10), Now);

        Assert.False(appointment.Cancel(Now));
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void CancellingWithLessThan24HoursNotice_IsLate()
    {
        var appointment = Appointment.Book(Ana, "Dr. Who", At(2, 15), Now);

        Assert.True(appointment.Cancel(Now));
    }

    [Fact]
    public void CancellingExactly24HoursBefore_IsNotLate()
    {
        var appointment = Appointment.Book(Ana, "Dr. Who", At(3, 10), Now);

        Assert.False(appointment.Cancel(At(2, 10)));
    }

    [Fact]
    public void AnAppointment_CannotBeCancelledTwice()
    {
        var appointment = Appointment.Book(Ana, "Dr. Who", At(4, 10), Now);
        appointment.Cancel(Now);

        Assert.Throws<RuleViolationException>(() => appointment.Cancel(Now));
    }

    // ---------- Invoice ----------

    [Fact]
    public void ALateCancellationInvoice_CarriesTheFeeAndTheReason()
    {
        var appointmentId = Guid.NewGuid();

        var invoice = Invoice.ForLateCancellation(Ana, appointmentId, Now);

        Assert.Equal((Invoice.LateCancellationFee, Ana, Now), (invoice.Amount, invoice.PatientId, invoice.IssuedAt));
        Assert.Contains(appointmentId.ToString(), invoice.Reason);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("Reason", 0)]
    [InlineData("Reason", -5)]
    public void AnInvoice_NeedsAReasonAndAPositiveAmount(string reason, double amount) =>
        Assert.Throws<RuleViolationException>(() => Invoice.Issue(Ana, reason, (decimal)amount, Now));
}
