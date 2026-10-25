using Gateway;

var builder = WebApplication.CreateBuilder(args);

// One entry point; the route table says which service owns which path prefix.
builder.Services.AddHttpClient("catalog", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101");
    c.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("orders", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Orders"] ?? "http://localhost:5102");
    c.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "gateway", status = "ok" }));
app.MapForwarding();

app.Run();
