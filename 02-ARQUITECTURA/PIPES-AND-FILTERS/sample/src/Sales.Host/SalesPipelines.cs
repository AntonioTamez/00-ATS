using Sales.Filters;
using Sales.Pipeline;

namespace Sales.Host;

/// <summary>Which filters run, and in what order. Changing a pipeline means changing only this file.</summary>
public static class SalesPipelines
{
    public static readonly IReadOnlyDictionary<string, decimal> VatRates = new Dictionary<string, decimal>
    {
        ["AR"] = 0.21m,
        ["ES"] = 0.21m,
        ["UY"] = 0.22m,
        ["CL"] = 0.19m,
    };

    public static readonly IReadOnlyList<string> BlockedSkus = ["BAN-001"];

    /// <summary>Everything: parse, validate, drop duplicates, drop blocked SKUs, price. <paramref name="buffer"/> &gt; 0 adds a buffered pipe after parsing.</summary>
    public static Pipeline<SourceLine, PricedSale> Standard(int buffer = 0)
    {
        var parsed = Pipelines.Of<SourceLine>().Then(new ParseCsvFilter());
        if (buffer > 0)
            parsed = parsed.Buffer(buffer);

        return parsed
            .Then(new ValidateSaleFilter())
            .Then(new DeduplicateFilter())
            .Then(new BlockedSkusFilter(BlockedSkus))
            .Then(new ApplyVatFilter(VatRates));
    }

    /// <summary>Trusts the input: no duplicate detection and no blocked SKUs. Reuses the same filters.</summary>
    public static Pipeline<SourceLine, PricedSale> Quick(int buffer = 0)
    {
        var parsed = Pipelines.Of<SourceLine>().Then(new ParseCsvFilter());
        if (buffer > 0)
            parsed = parsed.Buffer(buffer);

        return parsed
            .Then(new ValidateSaleFilter())
            .Then(new ApplyVatFilter(VatRates));
    }
}
