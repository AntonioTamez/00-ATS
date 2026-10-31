using Clinic.Appointments.Adapters;
using Clinic.Billing.Adapters;
using Clinic.Host;
using Clinic.Patients.Adapters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPatients();
builder.Services.AddAppointments();
builder.Services.AddBilling();
builder.Services.AddExceptionHandler<ClinicExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapPatients();
app.MapAppointments();
app.MapBilling();

app.Run();

public partial class Program;
