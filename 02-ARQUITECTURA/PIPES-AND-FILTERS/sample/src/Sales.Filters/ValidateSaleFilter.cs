using System.Globalization;
using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Filters;

/// <summary>Checks every field and turns the text into typed, normalized values (SKU and country upper-case).</summary>
public sealed class ValidateSaleFilter : IFilter<RawSale, Sale>
{
    public string Name => "validate-sale";

    public async IAsyncEnumerable<Sale> Run(
        IAsyncEnumerable<RawSale> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var raw in input.WithCancellation(cancellationToken))
        {
            var item = $"line {raw.Line}";

            if (raw.Id.Length == 0)
                rejects.Reject(item, "empty id");
            else if (raw.Sku.Length == 0)
                rejects.Reject(item, "empty sku");
            else if (!int.TryParse(raw.Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                rejects.Reject(item, $"quantity '{raw.Quantity}' is not a positive integer");
            else if (!decimal.TryParse(raw.UnitPrice, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) || price < 0)
                rejects.Reject(item, $"unit price '{raw.UnitPrice}' is not a non-negative number");
            else if (raw.Country.Length != 2 || !raw.Country.All(char.IsAsciiLetter))
                rejects.Reject(item, $"country '{raw.Country}' must be 2 letters");
            else
                yield return new Sale(
                    raw.Line, raw.Id, raw.Sku.ToUpperInvariant(), quantity, price, raw.Country.ToUpperInvariant());
        }
    }
}
