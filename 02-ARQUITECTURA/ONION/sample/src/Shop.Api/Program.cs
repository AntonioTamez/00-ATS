using Shop.Api;
using Shop.Application;
using Shop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the outermost ring is the only place that knows every ring.
builder.Services.AddApplication().AddInfrastructure();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapShopEndpoints();

app.Run();

public partial class Program;
