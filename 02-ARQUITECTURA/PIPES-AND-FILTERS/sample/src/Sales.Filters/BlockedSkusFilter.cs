using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Filters;

/// <summary>Refuses sales of SKUs that must not be sold. Same input and output types as the deduplicate filter, so the two can run in either order.</summary>
public sealed class BlockedSkusFilter : IFilter<Sale, Sale>
{
    private readonly IReadOnlySet<string> _blocked;

    public BlockedSkusFilter(IEnumerable<string> blockedSkus) =>
        _blocked = blockedSkus.Select(sku => sku.Trim().ToUpperInvariant()).ToHashSet();

    public string Name => "blocked-skus";

    public async IAsyncEnumerable<Sale> Run(
        IAsyncEnumerable<Sale> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sale in input.WithCancellation(cancellationToken))
        {
            if (_blocked.Contains(sale.Sku))
                rejects.Reject($"line {sale.Line}", $"sku '{sale.Sku}' is blocked");
            else
                yield return sale;
        }
    }
}
