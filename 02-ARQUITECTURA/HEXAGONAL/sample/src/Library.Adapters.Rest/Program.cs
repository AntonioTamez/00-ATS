using Library.Adapters.Outbound;
using Library.Adapters.Rest;
using Library.Core;

var builder = WebApplication.CreateBuilder(args);

// Composition root of the REST driving adapter: picks the core and the driven adapters.
builder.Services
    .AddLibraryCore()
    .AddOutboundAdapters(builder.Configuration["Library:JsonFile"]);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CoreExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapBookEndpoints();

app.Run();

public partial class Program;
