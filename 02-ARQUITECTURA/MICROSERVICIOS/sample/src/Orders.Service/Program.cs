using Orders.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OrderStore>();
builder.Services.AddScoped<OrderService>();

// The ONLY knowledge Orders has of Catalog: where it lives, and its HTTP contract (see CatalogClient).
builder.Services.AddHttpClient("catalog", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101");
    client.Timeout = TimeSpan.FromSeconds(2);   // never wait forever for another service
});
builder.Services.AddScoped<ICatalogClient, CatalogClient>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "orders", status = "ok" }));
app.MapOrdersEndpoints();

app.Run();
