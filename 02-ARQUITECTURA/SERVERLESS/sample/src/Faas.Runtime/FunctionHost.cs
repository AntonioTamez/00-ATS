namespace Faas.Runtime;

public sealed class HostOptions
{
    /// <summary>
    /// True: every invocation gets a brand-new function instance (worst-case "cold" platform).
    /// False: the platform keeps one warm instance per function. A correct function behaves the same either way.
    /// </summary>
    public bool RecycleInstances { get; init; }

    public TimeProvider Clock { get; init; } = TimeProvider.System;
}

public sealed class FunctionOptions
{
    /// <summary>The platform stops waiting for an invocation after this long and counts it as failed.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Queue triggers only: how many times a message is delivered before it is dead-lettered.</summary>
    public int MaxDeliveryCount { get; init; } = 3;

    /// <summary>Queue triggers only: how many messages of this function run at the same time.</summary>
    public int MaxConcurrency { get; init; } = 1;
}

public enum Outcome { Succeeded, Failed, TimedOut }

public sealed record InvocationRecord(
    string Function, string Trigger, string InvocationId, int Attempt, Outcome Outcome, string? Error);

/// <summary>
/// Runs functions the way a FaaS platform does: it decides when they run, gives each invocation a fresh context,
/// enforces the timeout, retries failed queue messages, dead-letters poison ones, and sends a function's output
/// messages only when the function succeeded. It never looks inside a function.
/// </summary>
public sealed class FunctionHost
{
    private readonly InMemoryMessageBus _bus;
    private readonly HostOptions _options;
    private readonly List<HttpRoute> _http = [];
    private readonly List<QueueBinding> _queues = [];
    private readonly Dictionary<string, Slot<ITimerFunction>> _timers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<int>> _coldStarts = new(StringComparer.Ordinal);
    private readonly List<InvocationRecord> _log = [];
    private readonly Lock _logGate = new();
    private int _invocationCounter;

    public FunctionHost(InMemoryMessageBus bus, HostOptions? options = null)
    {
        _bus = bus;
        _options = options ?? new HostOptions();
    }

    public IReadOnlyList<InvocationRecord> Invocations
    {
        get
        {
            lock (_logGate)
                return [.. _log];
        }
    }

    /// <summary>How many times the platform had to create an instance of this function.</summary>
    public int ColdStarts(string function) => _coldStarts[function]();

    // ---------- registration ----------

    public FunctionHost AddHttp(string name, string method, string path, Func<IHttpFunction> factory, FunctionOptions? options = null)
    {
        var slot = Register(name, factory, options);
        _http.Add(new HttpRoute(method, path, slot));
        return this;
    }

    public FunctionHost AddQueue(string name, string queue, Func<IQueueFunction> factory, FunctionOptions? options = null)
    {
        var slot = Register(name, factory, options);
        _queues.Add(new QueueBinding(queue, slot));
        return this;
    }

    public FunctionHost AddTimer(string name, Func<ITimerFunction> factory, FunctionOptions? options = null)
    {
        _timers[name] = Register(name, factory, options);
        return this;
    }

    private Slot<T> Register<T>(string name, Func<T> factory, FunctionOptions? options) where T : class
    {
        if (_coldStarts.ContainsKey(name))
            throw new ArgumentException($"a function named '{name}' is already registered", nameof(name));

        var o = options ?? new FunctionOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(o.MaxDeliveryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(o.MaxConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(o.Timeout, TimeSpan.Zero);

        var slot = new Slot<T>(name, factory, o);
        _coldStarts[name] = () => slot.ColdStarts;
        return slot;
    }

    // ---------- triggers ----------

    /// <summary>
    /// An HTTP request. A function that throws becomes a generic 500 (its message is never leaked) and one that
    /// times out becomes a 504. HTTP invocations are not retried: the client decides whether to call again.
    /// </summary>
    public async Task<HttpResponse> InvokeHttpAsync(HttpRequest request)
    {
        var route = _http.FirstOrDefault(r =>
            r.Method.Equals(request.Method, StringComparison.OrdinalIgnoreCase) && r.Path == request.Path);
        if (route is null)
            return new HttpResponse(404, """{"error":"not found"}""");

        var function = route.Slot.Acquire(_options.RecycleInstances);
        var run = await RunAsync(route.Slot.Options, 1, context => function.HandleAsync(request, context));
        Record(route.Slot.Name, "http", run.InvocationId, 1, run.Outcome, run.Error);

        switch (run.Outcome)
        {
            case Outcome.Succeeded:
                Apply(run.Result!.Outputs);
                return run.Result;
            case Outcome.TimedOut:
                return new HttpResponse(504, """{"error":"function timed out"}""");
            default:
                return new HttpResponse(500, """{"error":"internal error"}""");
        }
    }

    /// <summary>One tick of a timer. Failures are recorded and not retried: the next tick is the retry.</summary>
    public async Task<InvocationRecord> FireTimerAsync(string name)
    {
        if (!_timers.TryGetValue(name, out var slot))
            throw new ArgumentException($"no timer function named '{name}'", nameof(name));

        var function = slot.Acquire(_options.RecycleInstances);
        var scheduledAt = _options.Clock.GetUtcNow();
        var run = await RunAsync(slot.Options, 1, context => function.HandleAsync(new TimerInfo(scheduledAt), context));
        if (run.Outcome == Outcome.Succeeded)
            Apply(run.Result);
        return Record(slot.Name, "timer", run.InvocationId, 1, run.Outcome, run.Error);
    }

    /// <summary>
    /// Delivers queued messages to their functions until every queue is empty, including the messages that
    /// functions send to each other on the way. Returns how many invocations it took.
    /// </summary>
    public async Task<int> DrainQueuesAsync()
    {
        var invocations = 0;
        while (true)
        {
            var progressed = false;
            foreach (var binding in _queues)
            {
                var batch = _bus.Receive(binding.Queue, binding.Slot.Options.MaxConcurrency);
                if (batch.Count == 0)
                    continue;

                progressed = true;
                invocations += batch.Count;
                await Task.WhenAll(batch.Select(message => ProcessAsync(binding.Slot, message)));
            }

            if (!progressed)
                return invocations;
        }
    }

    private async Task ProcessAsync(Slot<IQueueFunction> slot, QueueMessage message)
    {
        var function = slot.Acquire(_options.RecycleInstances);
        var run = await RunAsync(slot.Options, message.DeliveryCount, context => function.HandleAsync(message, context));
        Record(slot.Name, "queue", run.InvocationId, message.DeliveryCount, run.Outcome, run.Error);

        if (run.Outcome == Outcome.Succeeded)
            Apply(run.Result);
        else if (message.DeliveryCount >= slot.Options.MaxDeliveryCount)
            _bus.SendToDeadLetter(message, run.Error ?? run.Outcome.ToString());
        else
            _bus.Abandon(message);
    }

    // ---------- one invocation ----------

    private sealed record Run<T>(Outcome Outcome, T? Result, string? Error, string InvocationId);

    private async Task<Run<T>> RunAsync<T>(FunctionOptions options, int attempt, Func<FunctionContext, Task<T>> call)
    {
        var id = $"inv-{Interlocked.Increment(ref _invocationCounter)}";
        var cancellation = new CancellationTokenSource();
        var context = new FunctionContext(id, attempt, _options.Clock.GetUtcNow(), cancellation.Token);
        var task = Task.Run(() => call(context));

        try
        {
            var result = await task.WaitAsync(options.Timeout);
            return new Run<T>(Outcome.Succeeded, result, null, id);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            // The platform stops waiting and asks the function to stop. A function that ignores the token keeps
            // running in the background: .NET cannot kill a task, which a real platform does by killing the instance.
            cancellation.Cancel();
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            return new Run<T>(Outcome.TimedOut, default, $"timed out after {options.Timeout.TotalMilliseconds:0} ms", id);
        }
        catch (OperationCanceledException)
        {
            return new Run<T>(Outcome.Failed, default, "cancelled", id);
        }
        catch (Exception ex)
        {
            return new Run<T>(Outcome.Failed, default, ex.Message, id);
        }
    }

    private void Apply(Outputs? outputs)
    {
        foreach (var message in outputs?.Messages ?? [])
            _bus.Send(message.Queue, message.Body, message.MessageId);
    }

    private InvocationRecord Record(string function, string trigger, string id, int attempt, Outcome outcome, string? error)
    {
        var record = new InvocationRecord(function, trigger, id, attempt, outcome, error);
        lock (_logGate)
            _log.Add(record);
        return record;
    }

    // ---------- bookkeeping ----------

    private sealed record HttpRoute(string Method, string Path, Slot<IHttpFunction> Slot);

    private sealed record QueueBinding(string Queue, Slot<IQueueFunction> Slot);

    /// <summary>The platform's cache of one function instance.</summary>
    private sealed class Slot<T>(string name, Func<T> factory, FunctionOptions options) where T : class
    {
        private readonly Lock _gate = new();
        private T? _instance;

        public string Name { get; } = name;

        public FunctionOptions Options { get; } = options;

        public int ColdStarts { get; private set; }

        public T Acquire(bool recycle)
        {
            lock (_gate)
            {
                if (recycle || _instance is null)
                {
                    _instance = factory();
                    ColdStarts++;
                }

                return _instance;
            }
        }
    }
}
