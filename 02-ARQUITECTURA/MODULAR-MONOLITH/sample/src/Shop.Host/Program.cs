using Catalog.Core;
using Notifications.Core;
using Ordering.Core;
using Shop.SharedKernel;

// Composition root: the one place that knows every module exists.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharedKernel();
builder.Services.AddCatalogModule();
builder.Services.AddOrderingModule();
builder.Services.AddNotificationsModule();

var app = builder.Build();

app.MapCatalogEndpoints();
app.MapOrderingEndpoints();
app.MapNotificationsEndpoints();

app.Run();

public partial class Program;
