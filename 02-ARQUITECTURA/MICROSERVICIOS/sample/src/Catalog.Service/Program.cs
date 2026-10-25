using Catalog.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<StockStore>();   // this service's OWN data: nobody else can reach it

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "catalog", status = "ok" }));
app.MapCatalogEndpoints();

app.Run();
