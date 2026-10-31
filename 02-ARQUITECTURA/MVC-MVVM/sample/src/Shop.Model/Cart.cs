namespace Shop.Model;

public sealed record Product(string Sku, string Name, decimal Price);

public sealed class CartRuleException(string message) : Exception(message);

public sealed class Catalog
{
    private readonly List<Product> _products =
    [
        new("BOOK-1", "Clean Architecture (book)", 35.00m),
        new("MUG-1", "Architecture mug", 9.50m),
        new("PEN-1", "Pen", 1.50m),
    ];

    public IReadOnlyList<Product> All => _products;

    public Product? Find(string sku) =>
        _products.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));
}

public sealed record CartLine(Product Product, int Quantity)
{
    public decimal Total => Product.Price * Quantity;
}

/// <summary>
/// The domain. It holds the rules (quantity limit, discount) and knows nothing about screens:
/// the same class serves the MVC web app and the MVVM view model.
/// </summary>
public sealed class Cart
{
    public const int MaxPerLine = 10;
    public const decimal DiscountThreshold = 100m;
    public const decimal DiscountRate = 0.10m;

    private readonly List<CartLine> _lines = [];

    public IReadOnlyList<CartLine> Lines => _lines;

    public int ItemCount => _lines.Sum(l => l.Quantity);

    public decimal Subtotal => _lines.Sum(l => l.Total);

    public decimal Discount => Subtotal >= DiscountThreshold ? Math.Round(Subtotal * DiscountRate, 2) : 0m;

    public decimal Total => Subtotal - Discount;

    public void Add(Product product, int quantity)
    {
        if (quantity <= 0) throw new CartRuleException("The quantity must be positive.");

        var index = _lines.FindIndex(l => l.Product.Sku == product.Sku);
        var newQuantity = (index >= 0 ? _lines[index].Quantity : 0) + quantity;
        if (newQuantity > MaxPerLine)
            throw new CartRuleException($"You can have at most {MaxPerLine} units of {product.Name}.");

        if (index >= 0) _lines[index] = _lines[index] with { Quantity = newQuantity };
        else _lines.Add(new CartLine(product, newQuantity));
    }

    /// <returns>false when the product was not in the cart.</returns>
    public bool Remove(string sku) => _lines.RemoveAll(l => string.Equals(l.Product.Sku, sku, StringComparison.OrdinalIgnoreCase)) > 0;

    public void Clear() => _lines.Clear();
}
