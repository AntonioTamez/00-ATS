using System.Text.Json;

namespace Eda.Messaging;

/// <summary>
/// What travels through the broker: an envelope with a string type and a JSON payload.
/// Producers and consumers each own their own C# types for the payload: they share a message format, not code.
/// </summary>
public sealed record Message(Guid Id, string Type, string Payload, DateTimeOffset OccurredAt, string CorrelationId)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Message Create<TPayload>(string type, TPayload payload, string correlationId, DateTimeOffset now) =>
        new(Guid.NewGuid(), type, JsonSerializer.Serialize(payload, Options), now, correlationId);

    /// <summary>Reads only the fields the consumer cares about; unknown fields are ignored (tolerant reader).</summary>
    public TPayload Read<TPayload>() =>
        JsonSerializer.Deserialize<TPayload>(Payload, Options) ?? throw new JsonException("The message payload is empty.");
}
