using Faas.Runtime;

namespace Orders.Tests;

internal static class Support
{
    public static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

    public static string SolutionRoot { get; } = FindSolutionRoot();

    public static string SourceFolder(string project) => Path.Combine(SolutionRoot, "src", project);

    private static string FindSolutionRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Orders.slnx")))
                return dir.FullName;
        throw new InvalidOperationException("Orders.slnx not found above " + AppContext.BaseDirectory);
    }

    public static FunctionContext Context(int attempt = 1) => new("test-invocation", attempt, Now, CancellationToken.None);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Functions written for the platform tests: they only know the platform contracts, no orders anywhere.</summary>
internal sealed class QueueFn(Func<QueueMessage, FunctionContext, Task<Outputs>> handle) : IQueueFunction
{
    public Task<Outputs> HandleAsync(QueueMessage message, FunctionContext context) => handle(message, context);
}

internal sealed class HttpFn(Func<HttpRequest, FunctionContext, Task<HttpResponse>> handle) : IHttpFunction
{
    public Task<HttpResponse> HandleAsync(HttpRequest request, FunctionContext context) => handle(request, context);
}

internal sealed class TimerFn(Func<TimerInfo, FunctionContext, Task<Outputs>> handle) : ITimerFunction
{
    public Task<Outputs> HandleAsync(TimerInfo timer, FunctionContext context) => handle(timer, context);
}

/// <summary>Counts how many invocations are running at the same time.</summary>
internal sealed class ConcurrencyProbe
{
    private int _current;
    private int _max;

    public int Max => Volatile.Read(ref _max);

    public async Task HoldAsync(int milliseconds)
    {
        var now = Interlocked.Increment(ref _current);
        int seen;
        while (now > (seen = Volatile.Read(ref _max)) && Interlocked.CompareExchange(ref _max, now, seen) != seen)
        {
        }

        await Task.Delay(milliseconds);
        Interlocked.Decrement(ref _current);
    }
}
