using System.Text.Json;
using System.Text.Json.Nodes;
using Ledger.Domain;

namespace Ledger.Infrastructure;

/// <summary>
/// Turns domain events into stored JSON and back. Stored events are NEVER rewritten: when an event's shape
/// changes, an UPCASTER converts old stored data to the current shape at read time.
/// </summary>
public sealed class EventSerializer
{
    private sealed record Registration(string TypeName, Type ClrType, int CurrentVersion);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<Type, Registration> _byClr = [];
    private readonly Dictionary<string, Registration> _byName = [];
    private readonly Dictionary<(string Type, int From), Func<JsonObject, JsonObject>> _upcasters = [];

    public IReadOnlyCollection<Type> RegisteredTypes => _byClr.Keys;

    public EventSerializer Register<TEvent>(string typeName, int currentVersion = 1) where TEvent : IDomainEvent
    {
        var registration = new Registration(typeName, typeof(TEvent), currentVersion);
        _byClr[typeof(TEvent)] = registration;
        _byName[typeName] = registration;
        return this;
    }

    /// <summary>Teaches the serializer how to turn version <paramref name="fromVersion"/> data into version+1 data.</summary>
    public EventSerializer Upcast(string typeName, int fromVersion, Func<JsonObject, JsonObject> upcaster)
    {
        _upcasters[(typeName, fromVersion)] = upcaster;
        return this;
    }

    public NewEvent Serialize(IDomainEvent e)
    {
        if (!_byClr.TryGetValue(e.GetType(), out var registration))
            throw new InvalidOperationException($"Event type {e.GetType().Name} is not registered.");

        return new NewEvent(registration.TypeName, registration.CurrentVersion, JsonSerializer.Serialize(e, e.GetType(), Options));
    }

    public IDomainEvent Deserialize(StoredEvent stored)
    {
        if (!_byName.TryGetValue(stored.Type, out var registration))
            throw new InvalidOperationException($"Unknown stored event type '{stored.Type}'.");
        if (stored.SchemaVersion > registration.CurrentVersion)
            throw new InvalidOperationException(
                $"'{stored.Type}' was written with schema v{stored.SchemaVersion}, newer than v{registration.CurrentVersion} known here.");

        var node = JsonNode.Parse(stored.Data)?.AsObject() ?? throw new InvalidOperationException("Empty event data.");
        for (var version = stored.SchemaVersion; version < registration.CurrentVersion; version++)
        {
            if (!_upcasters.TryGetValue((stored.Type, version), out var upcaster))
                throw new InvalidOperationException($"No upcaster from '{stored.Type}' v{version} to v{version + 1}.");
            node = upcaster(node);
        }

        return (IDomainEvent)(node.Deserialize(registration.ClrType, Options)
            ?? throw new InvalidOperationException("The event data could not be read."));
    }

    /// <summary>The registry for this domain. Every event type MUST be listed here (a test enforces it).</summary>
    public static EventSerializer CreateDefault() => new EventSerializer()
        .Register<ItemRegistered>("item-registered")
        .Register<StockReceived>("stock-received", currentVersion: 2)
        .Register<StockShipped>("stock-shipped")
        .Register<StockAdjusted>("stock-adjusted")
        .Register<ItemDiscontinued>("item-discontinued")
        // History: stock-received v1 stored {"qty":5}. v2 renamed it to "quantity" and added "supplier".
        .Upcast("stock-received", fromVersion: 1, v1 => new JsonObject
        {
            ["quantity"] = v1["qty"]?.DeepClone(),
            ["supplier"] = null,
        });
}
