using Microsoft.AspNetCore.Mvc;
using Shop.Model;
using Shop.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    // Every POST must carry an anti-forgery token (CSRF protection). The forms get it automatically.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddSingleton<Catalog>();
builder.Services.AddSingleton<CartStore>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();

public partial class Program;
