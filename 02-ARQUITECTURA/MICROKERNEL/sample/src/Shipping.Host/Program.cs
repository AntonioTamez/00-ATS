using System.Globalization;
using Shipping.Contracts;
using Shipping.Kernel;

if (args.Length != 3
    || !decimal.TryParse(args[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var weightKg)
    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var distanceKm))
{
    Console.Error.WriteLine("usage: Shipping.Host <plugins-dir> <weight-kg> <distance-km>");
    return 2;
}

using var catalog = PluginCatalog.Load(args[0]);

Console.WriteLine($"Plugins loaded: {string.Join(", ", catalog.Carriers.Select(c => c.Id).DefaultIfEmpty("(none)"))}");
foreach (var rejected in catalog.Rejected)
    Console.WriteLine($"Plugin rejected: {rejected.Source} - {rejected.Reason}");

var result = new QuoteService(catalog).GetQuotes(new Parcel(weightKg, distanceKm));
foreach (var quote in result.Quotes)
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {quote.CarrierId,-10} {quote.Price,8:0.00}  {quote.Days} day(s)"));
foreach (var failure in result.Failures)
    Console.WriteLine($"  {failure.CarrierId,-10} FAILED: {failure.Reason}");

Console.WriteLine(result.Cheapest is { } best ? $"Cheapest: {best.CarrierId}" : "Nobody serves this parcel.");
return result.Cheapest is null ? 1 : 0;
