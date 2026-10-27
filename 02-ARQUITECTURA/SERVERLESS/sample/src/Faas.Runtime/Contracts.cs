namespace Faas.Runtime;

// ---------- what a function receives ----------

/// <summary>Everything the platform gives ONE invocation. A new context per invocation: nothing in it survives.</summary>
public sealed record FunctionContext(string InvocationId, int Attempt, DateTimeOffset Now, CancellationToken CancellationToken);

public sealed record QueueMessage(string Id, string Queue, string Body, int DeliveryCount);

public sealed record HttpRequest(string Method, string Path, string Body, IReadOnlyDictionary<string, string>? Headers = null)
{
    public string? Header(string name) =>
        Headers?.FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}

public sealed record TimerInfo(DateTimeOffset ScheduledAt);

// ---------- what a function returns ----------

/// <summary>A message for an output binding. The platform sends it only if the function finishes without failing.</summary>
public sealed record OutgoingMessage(string Queue, string Body, string? MessageId = null);

public sealed record Outputs(IReadOnlyList<OutgoingMessage> Messages)
{
    public static Outputs None { get; } = new([]);

    public static Outputs Send(params OutgoingMessage[] messages) => new(messages);
}

public sealed record HttpResponse(int Status, string Body, Outputs? Outputs = null);

// ---------- the three trigger kinds ----------

public interface IQueueFunction
{
    Task<Outputs> HandleAsync(QueueMessage message, FunctionContext context);
}

public interface IHttpFunction
{
    Task<HttpResponse> HandleAsync(HttpRequest request, FunctionContext context);
}

public interface ITimerFunction
{
    Task<Outputs> HandleAsync(TimerInfo timer, FunctionContext context);
}
