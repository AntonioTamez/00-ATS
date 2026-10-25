using Helpdesk.Api.Common;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase(builder.Configuration["Database:Name"] ?? "helpdesk"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFeatures();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapFeatures();

app.Run();

public partial class Program;
