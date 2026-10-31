using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Filters;

/// <summary>Calculates net, VAT and total. Amounts are rounded to 2 decimals, halves away from zero.</summary>
public sealed class ApplyVatFilter : IFilter<Sale, PricedSale>
{
    private readonly IReadOnlyDictionary<string, decimal> _rateByCountry;

    public ApplyVatFilter(IReadOnlyDictionary<string, decimal> rateByCountry) => _rateByCountry = rateByCountry;

    public string Name => "apply-vat";

    public async IAsyncEnumerable<PricedSale> Run(
        IAsyncEnumerable<Sale> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sale in input.WithCancellation(cancellationToken))
        {
            if (!_rateByCountry.TryGetValue(sale.Country, out var rate))
            {
                rejects.Reject($"line {sale.Line}", $"no VAT rate for country '{sale.Country}'");
                continue;
            }

            var net = Math.Round(sale.Quantity * sale.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(net * rate, 2, MidpointRounding.AwayFromZero);
            yield return new PricedSale(sale.Line, sale.Id, sale.Sku, sale.Quantity, sale.Country, net, vat, net + vat);
        }
    }
}
