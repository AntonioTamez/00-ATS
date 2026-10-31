namespace Sales.Pipeline;

/// <summary>
/// One stage of a pipeline. It reads items from its inbound pipe and writes items to its outbound pipe.
/// A filter knows its input and output types and nothing else: not the other filters, not the order they run in.
/// </summary>
public interface IFilter<TIn, TOut>
{
    /// <summary>Stable kebab-case name, used in rejections and failures.</summary>
    string Name { get; }

    /// <summary>
    /// Transforms the inbound stream lazily. An item that cannot continue is reported with
    /// <paramref name="rejects"/> and simply not written to the outbound pipe.
    /// </summary>
    IAsyncEnumerable<TOut> Run(IAsyncEnumerable<TIn> input, IRejectSink rejects, CancellationToken cancellationToken);
}

/// <summary>Where a filter reports the items it refuses. The pipeline binds the filter's name to it.</summary>
public interface IRejectSink
{
    void Reject(string item, string reason);
}

/// <summary>An item a filter refused, with who refused it and why.</summary>
public sealed record Rejection(string Filter, string Item, string Reason);

/// <summary>A filter threw. The whole run fails: a pipeline is only as reliable as its weakest stage.</summary>
public sealed class PipelineException(string filter, Exception inner)
    : Exception($"filter '{filter}' failed: {inner.Message}", inner)
{
    public string Filter { get; } = filter;
}
