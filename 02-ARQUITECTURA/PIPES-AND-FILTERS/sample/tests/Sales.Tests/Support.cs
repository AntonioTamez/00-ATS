using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Tests;

internal static class Support
{
    /// <summary>The folder that holds Sales.slnx, found by walking up from the test binaries.</summary>
    public static string SolutionRoot { get; } = FindSolutionRoot();

    public static string SampleCsv => Path.Combine(SolutionRoot, "data", "sales.csv");

    public static string SourceFolder(string project) => Path.Combine(SolutionRoot, "src", project);

    private static string FindSolutionRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Sales.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Sales.slnx not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Runs one filter alone, on the given items, and returns what came out and what was rejected.</summary>
    public static async Task<(List<TOut> Output, IReadOnlyList<Rejection> Rejected)> RunAlone<TIn, TOut>(
        IFilter<TIn, TOut> filter, params TIn[] items)
    {
        var run = Pipelines.Of<TIn>().Then(filter).Start(items.ToAsyncEnumerable());
        var output = await run.Output.ToListAsync();
        return (output, run.Rejections);
    }
}

/// <summary>A filter written for the tests: applies a function to every item. Proves the core needs no domain.</summary>
internal sealed class Map<TIn, TOut>(string name, Func<TIn, TOut> map) : IFilter<TIn, TOut>
{
    public string Name => name;

    public async IAsyncEnumerable<TOut> Run(
        IAsyncEnumerable<TIn> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
            yield return map(item);
    }
}

/// <summary>Refuses the items the predicate matches, passes the rest.</summary>
internal sealed class RejectIf<T>(string name, Func<T, bool> refuse, string reason) : IFilter<T, T>
{
    public string Name => name;

    public async IAsyncEnumerable<T> Run(
        IAsyncEnumerable<T> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            if (refuse(item))
                rejects.Reject($"item {item}", reason);
            else
                yield return item;
        }
    }
}

/// <summary>Throws when it meets <paramref name="failAt"/>.</summary>
internal sealed class FailAt(string name, int failAt) : IFilter<int, int>
{
    public string Name => name;

    public async IAsyncEnumerable<int> Run(
        IAsyncEnumerable<int> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            if (item == failAt)
                throw new InvalidOperationException("boom");
            yield return item;
        }
    }
}

/// <summary>A source that counts how far the pipeline has pulled and how many items are in flight.</summary>
internal sealed class Probe
{
    private int _produced;
    private int _consumed;
    private int _maxInFlight;
    private int _disposed;

    public int Produced => Volatile.Read(ref _produced);
    public int Consumed => Volatile.Read(ref _consumed);
    public int MaxInFlight => Volatile.Read(ref _maxInFlight);
    public bool Disposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>Call from the consumer for every item it receives.</summary>
    public void Received() => Interlocked.Increment(ref _consumed);

    public async IAsyncEnumerable<int> Source(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var produced = Interlocked.Increment(ref _produced);
                var inFlight = produced - Consumed;
                int seen;
                while (inFlight > (seen = MaxInFlight) && Interlocked.CompareExchange(ref _maxInFlight, inFlight, seen) != seen) { }
                await Task.Yield();
                yield return i;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _disposed, 1);
        }
    }
}
