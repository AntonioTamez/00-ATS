namespace Sales.Pipeline;

/// <summary>Entry point: start from <c>Of&lt;SourceType&gt;</c> and chain filters one after another.</summary>
public static class Pipelines
{
    public static Pipeline<T, T> Of<T>() => new((source, _, _) => source, []);
}

/// <summary>
/// An immutable chain of filters from <typeparamref name="TIn"/> to <typeparamref name="TOut"/>.
/// <see cref="Then{TNext}"/> only accepts a filter whose input type is the current output type, so a chain
/// whose schemas do not match does not compile. A pipeline can be started any number of times.
/// </summary>
public sealed class Pipeline<TIn, TOut>
{
    private readonly Func<IAsyncEnumerable<TIn>, RejectLog, CancellationToken, IAsyncEnumerable<TOut>> _compose;

    internal Pipeline(
        Func<IAsyncEnumerable<TIn>, RejectLog, CancellationToken, IAsyncEnumerable<TOut>> compose,
        IReadOnlyList<string> stages)
    {
        _compose = compose;
        Stages = stages;
    }

    /// <summary>The stage names in order, buffers included.</summary>
    public IReadOnlyList<string> Stages { get; }

    public Pipeline<TIn, TNext> Then<TNext>(IFilter<TOut, TNext> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var upstream = _compose;
        return new Pipeline<TIn, TNext>(
            (source, log, ct) => Wiring.Guard(filter.Name, filter.Run(upstream(source, log, ct), log.For(filter.Name), ct), ct),
            [.. Stages, filter.Name]);
    }

    /// <summary>Puts a bounded buffer in the pipe after the current last stage.</summary>
    public Pipeline<TIn, TOut> Buffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        var upstream = _compose;
        return new Pipeline<TIn, TOut>(
            (source, log, ct) => Wiring.Buffer(upstream(source, log, ct), capacity, ct),
            [.. Stages, $"buffer({capacity})"]);
    }

    /// <summary>Starts a run. Nothing is read from <paramref name="source"/> until the output is enumerated.</summary>
    public PipelineRun<TOut> Start(IAsyncEnumerable<TIn> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var log = new RejectLog();
        return new PipelineRun<TOut>(_compose(source, log, cancellationToken), log);
    }
}

/// <summary>One run of a pipeline: the lazy output and the rejections collected so far.</summary>
public sealed class PipelineRun<TOut>
{
    private readonly RejectLog _log;

    internal PipelineRun(IAsyncEnumerable<TOut> output, RejectLog log)
    {
        Output = output;
        _log = log;
    }

    /// <summary>Enumerating it is what makes the pipeline work: items are pulled through, one at a time.</summary>
    public IAsyncEnumerable<TOut> Output { get; }

    /// <summary>Everything refused so far, in the order it happened.</summary>
    public IReadOnlyList<Rejection> Rejections => _log.Snapshot();
}
