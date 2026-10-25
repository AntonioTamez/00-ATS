using Library.Adapters.Cli;
using Library.Adapters.Outbound;
using Library.Core;
using Microsoft.Extensions.DependencyInjection;

// Composition root of the CLI driving adapter. Data persists in a JSON file between runs.
var file = Environment.GetEnvironmentVariable("LIBRARY_FILE") ?? Path.Combine(AppContext.BaseDirectory, "library.json");

var services = new ServiceCollection()
    .AddLibraryCore()
    .AddOutboundAdapters(file)
    .AddSingleton(Console.Out)
    .AddScoped<CliRunner>()
    .BuildServiceProvider();

using var scope = services.CreateScope();
return await scope.ServiceProvider.GetRequiredService<CliRunner>().RunAsync(args);
