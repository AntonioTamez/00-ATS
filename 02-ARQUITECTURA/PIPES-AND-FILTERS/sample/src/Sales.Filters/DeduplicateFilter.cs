using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Filters;

/// <summary>
/// Drops a sale whose id was already seen in this run. The "already seen" memory is created inside
/// <see cref="Run"/>, never kept in the filter, so the same filter can serve any number of runs.
/// </summary>
public sealed class DeduplicateFilter : IFilter<Sale, Sale>
{
    public string Name => "deduplicate";

    public async IAsyncEnumerable<Sale> Run(
        IAsyncEnumerable<Sale> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var firstSeenAt = new Dictionary<string, int>(StringComparer.Ordinal);

        await foreach (var sale in input.WithCancellation(cancellationToken))
        {
            if (firstSeenAt.TryGetValue(sale.Id, out var first))
            {
                rejects.Reject($"line {sale.Line}", $"duplicate id '{sale.Id}' (first seen at line {first})");
                continue;
            }

            firstSeenAt[sale.Id] = sale.Line;
            yield return sale;
        }
    }
}
