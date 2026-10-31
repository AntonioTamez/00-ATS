using Hotel.Api;
using Hotel.Application;
using Hotel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the only place that knows every layer.
builder.Services.AddApplication().AddInfrastructure();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapHotelEndpoints();

app.Run();

public partial class Program;
