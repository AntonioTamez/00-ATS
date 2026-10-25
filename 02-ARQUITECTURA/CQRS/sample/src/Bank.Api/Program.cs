using Bank.Api;
using Bank.Commands;
using Bank.Infrastructure;
using Bank.Queries;

// Composition root.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCommands();
builder.Services.AddQueries();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<ProjectionWorker>();
builder.Services.AddExceptionHandler<BankExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapBankEndpoints();

app.Run();

public partial class Program;
