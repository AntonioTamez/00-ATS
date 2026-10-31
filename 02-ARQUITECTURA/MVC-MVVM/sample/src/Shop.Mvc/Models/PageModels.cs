namespace Shop.Mvc.Models;

// In MVC a "view model" is a PASSIVE bag of data shaped for one view: no behavior, no notifications, no commands.
// The controller fills it and hands it to the view, once, per request. (In MVVM the view model is ACTIVE: see Shop.ViewModels.)

public sealed record ProductCard(string Sku, string Name, string PriceText);

public sealed record ProductsIndexViewModel(IReadOnlyList<ProductCard> Products, int CartItemCount);

public sealed record CartLineView(string Sku, string Name, int Quantity, string UnitPriceText, string LineTotalText);

public sealed record CartIndexViewModel(
    IReadOnlyList<CartLineView> Lines,
    string SubtotalText,
    string DiscountText,
    string TotalText,
    bool IsEmpty,
    string? Error);
