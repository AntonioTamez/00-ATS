using Clinic.Technical.Controllers;
using Clinic.Technical.Repositories;
using Clinic.Technical.Services;

// BAD EXAMPLE: same domain as ../Clinic, organized by technical role. See Clinic.Technical.csproj.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PatientRepository>();
builder.Services.AddSingleton<AppointmentRepository>();
builder.Services.AddScoped<PatientService>();
builder.Services.AddScoped<AppointmentService>();

var app = builder.Build();

PatientController.Map(app);
AppointmentController.Map(app);

app.Run();
