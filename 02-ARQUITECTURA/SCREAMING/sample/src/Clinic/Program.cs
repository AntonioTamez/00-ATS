using Clinic;
using Clinic.Appointments;
using Clinic.Billing;
using Clinic.Patients;

// The web framework is a detail at the EDGE of the system. What this program is made of is on the folder tree:
// Patients, Appointments, Billing.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPatients();
builder.Services.AddAppointments();
builder.Services.AddBilling();
builder.Services.AddClinicErrorHandling();

var app = builder.Build();

app.UseExceptionHandler();
app.MapPatients();
app.MapAppointments();
app.MapBilling();

app.Run();

public partial class Program;
