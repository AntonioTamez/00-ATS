namespace Ledger.Domain;

public sealed class DomainException(string message) : Exception(message);

/// <summary>A fact that already happened. Named in the past tense; never edited, never deleted.</summary>
public interface IDomainEvent;

// Events carry the BUSINESS INTENT ("stock was received from a supplier"), not just the resulting number.
public sealed record ItemRegistered(string Sku, string Name) : IDomainEvent;
public sealed record StockReceived(int Quantity, string? Supplier) : IDomainEvent;
public sealed record StockShipped(int Quantity, string Reference) : IDomainEvent;
public sealed record StockAdjusted(int Delta, string Reason) : IDomainEvent;
public sealed record ItemDiscontinued : IDomainEvent;

/// <summary>A cached copy of the state at a given version. It is an optimization, never the source of truth.</summary>
public sealed record StockItemSnapshot(string Sku, string Name, int OnHand, bool Discontinued, int Version);

/// <summary>
/// An event-sourced aggregate. Its state is NOT stored: it is rebuilt by replaying events.
/// Commands decide (and may refuse); <see cref="Apply"/> only records what already happened and never refuses.
/// </summary>
public sealed class StockItem
{
    private readonly List<IDomainEvent> _uncommitted = [];

    private StockItem() { }

    public string Sku { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int OnHand { get; private set; }
    public bool Discontinued { get; private set; }

    /// <summary>How many events have shaped this state (committed or not).</summary>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> Uncommitted => _uncommitted;

    // ------------------------------------------------------------ commands: decide, then raise an event

    public static StockItem Register(string sku, string name)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("The SKU is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("The name is required.");

        var item = new StockItem();
        item.Raise(new ItemRegistered(sku.Trim().ToUpperInvariant(), name.Trim()));
        return item;
    }

    public void Receive(int quantity, string? supplier)
    {
        EnsureActive();
        if (quantity <= 0) throw new DomainException("The received quantity must be positive.");
        Raise(new StockReceived(quantity, string.IsNullOrWhiteSpace(supplier) ? null : supplier.Trim()));
    }

    public void Ship(int quantity, string reference)
    {
        EnsureActive();
        if (quantity <= 0) throw new DomainException("The shipped quantity must be positive.");
        if (string.IsNullOrWhiteSpace(reference)) throw new DomainException("A shipment reference is required.");
        if (quantity > OnHand) throw new DomainException($"Cannot ship {quantity}: only {OnHand} on hand.");
        Raise(new StockShipped(quantity, reference.Trim()));
    }

    /// <summary>Corrections are NEW events. The mistake stays in the history; the adjustment explains how it was fixed.</summary>
    public void Adjust(int delta, string reason)
    {
        EnsureActive();
        if (delta == 0) throw new DomainException("An adjustment cannot be zero.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("An adjustment needs a reason.");
        if (OnHand + delta < 0) throw new DomainException($"The adjustment would leave {OnHand + delta} on hand.");
        Raise(new StockAdjusted(delta, reason.Trim()));
    }

    public void Discontinue()
    {
        EnsureActive();
        if (OnHand != 0) throw new DomainException("Only an item with no stock can be discontinued.");
        Raise(new ItemDiscontinued());
    }

    // ------------------------------------------------------------ rebuilding state

    /// <summary>Rebuilds an item from its whole history. This is "rehydration".</summary>
    public static StockItem Rehydrate(IEnumerable<IDomainEvent> history)
    {
        var item = new StockItem();
        foreach (var e in history) item.Apply(e);
        return item;
    }

    /// <summary>Rebuilds an item from a snapshot plus only the events that came after it.</summary>
    public static StockItem Rehydrate(StockItemSnapshot snapshot, IEnumerable<IDomainEvent> eventsAfterSnapshot)
    {
        var item = new StockItem
        {
            Sku = snapshot.Sku,
            Name = snapshot.Name,
            OnHand = snapshot.OnHand,
            Discontinued = snapshot.Discontinued,
            Version = snapshot.Version,
        };
        foreach (var e in eventsAfterSnapshot) item.Apply(e);
        return item;
    }

    public StockItemSnapshot ToSnapshot() => new(Sku, Name, OnHand, Discontinued, Version);

    public IReadOnlyList<IDomainEvent> PullUncommitted()
    {
        var pending = _uncommitted.ToList();
        _uncommitted.Clear();
        return pending;
    }

    // ------------------------------------------------------------ internals

    private void Raise(IDomainEvent e)
    {
        Apply(e);
        _uncommitted.Add(e);
    }

    /// <summary>Applies a FACT to the state. It never validates: history cannot be argued with.</summary>
    private void Apply(IDomainEvent e)
    {
        switch (e)
        {
            case ItemRegistered r: Sku = r.Sku; Name = r.Name; break;
            case StockReceived r: OnHand += r.Quantity; break;
            case StockShipped s: OnHand -= s.Quantity; break;
            case StockAdjusted a: OnHand += a.Delta; break;
            case ItemDiscontinued: Discontinued = true; break;
            default: throw new InvalidOperationException($"Unknown event {e.GetType().Name}.");
        }
        Version++;
    }

    private void EnsureActive()
    {
        if (Discontinued) throw new DomainException("The item is discontinued.");
    }
}
