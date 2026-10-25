namespace Shop.Domain.Model;

/// <summary>Entity at the center of the onion: state and behavior only, no knowledge of the outside.</summary>
public sealed class Customer
{
    public const int LoyaltyThreshold = 3;

    public Guid Id { get; }
    public string Name { get; }
    public int CompletedOrders { get; private set; }
    public bool IsLoyal => CompletedOrders >= LoyaltyThreshold;

    private Customer(Guid id, string name, int completedOrders)
    {
        Id = id;
        Name = name;
        CompletedOrders = completedOrders;
    }

    public static Customer Create(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new DomainException("A customer name is required.");
        return new Customer(Guid.NewGuid(), normalized, 0);
    }

    /// <summary>Rebuilds a stored customer. For infrastructure adapters, never to create new ones.</summary>
    public static Customer Rehydrate(Guid id, string name, int completedOrders) => new(id, name, completedOrders);

    public void RegisterPlacedOrder() => CompletedOrders++;
}
