namespace Catalog.Core;

internal sealed class Product
{
    private readonly object _gate = new();

    public Product(string sku, string name, decimal price, int stock)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (stock < 0) throw new ArgumentOutOfRangeException(nameof(stock));

        Sku = sku;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public int Stock { get; private set; }

    /// <returns>An error message, or null when the stock was reserved.</returns>
    public string? TryReserve(int quantity)
    {
        if (quantity <= 0) return "Quantity must be positive.";

        lock (_gate)
        {
            if (Stock < quantity) return $"Not enough stock for {Sku}: requested {quantity}, available {Stock}.";
            Stock -= quantity;
            return null;
        }
    }

    public void Release(int quantity)
    {
        if (quantity <= 0) return;
        lock (_gate) Stock += quantity;
    }
}
