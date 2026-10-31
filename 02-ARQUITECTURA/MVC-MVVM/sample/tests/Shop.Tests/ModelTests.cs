using Shop.Model;

namespace Shop.Tests;

/// <summary>The Model alone: the business rules, with no screen of any kind anywhere in sight.</summary>
public class ModelTests
{
    private static readonly Catalog Catalog = new();
    private static Product Book => Catalog.Find("BOOK-1")!;
    private static Product Mug => Catalog.Find("MUG-1")!;
    private static Product Pen => Catalog.Find("PEN-1")!;

    [Fact]
    public void Catalog_FindsProducts_IgnoringCase()
    {
        Assert.Equal("MUG-1", Catalog.Find("mug-1")!.Sku);
        Assert.Null(Catalog.Find("NOPE"));
    }

    [Fact]
    public void AnEmptyCart_IsZeroEverywhere()
    {
        var cart = new Cart();

        Assert.Equal((0, 0m, 0m, 0m), (cart.ItemCount, cart.Subtotal, cart.Discount, cart.Total));
    }

    [Fact]
    public void AddingTheSameProductTwice_MergesTheLine()
    {
        var cart = new Cart();

        cart.Add(Pen, 2);
        cart.Add(Pen, 3);

        var line = Assert.Single(cart.Lines);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(7.50m, line.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveQuantity_IsRefused(int quantity) =>
        Assert.Throws<CartRuleException>(() => new Cart().Add(Pen, quantity));

    [Fact]
    public void ALine_CannotExceedTheMaximum_EvenAcrossSeveralAdds()
    {
        var cart = new Cart();
        cart.Add(Pen, 8);

        var ex = Assert.Throws<CartRuleException>(() => cart.Add(Pen, 3));

        Assert.Contains($"{Cart.MaxPerLine}", ex.Message);
        Assert.Equal(8, cart.Lines.Single().Quantity);          // the refused add left no trace
    }

    [Fact]
    public void ALineAtTheMaximum_IsAllowed()
    {
        var cart = new Cart();

        cart.Add(Pen, Cart.MaxPerLine);

        Assert.Equal(Cart.MaxPerLine, cart.ItemCount);
    }

    [Fact]
    public void BelowTheThreshold_ThereIsNoDiscount()
    {
        var cart = new Cart();
        cart.Add(Book, 2);                                       // 70.00

        Assert.Equal((70m, 0m, 70m), (cart.Subtotal, cart.Discount, cart.Total));
    }

    [Fact]
    public void AboveTheThreshold_TenPercentIsDiscounted()
    {
        var cart = new Cart();
        cart.Add(Book, 3);                                       // 105.00

        Assert.Equal((105m, 10.50m, 94.50m), (cart.Subtotal, cart.Discount, cart.Total));
    }

    [Fact]
    public void ExactlyAtTheThreshold_TheDiscountApplies()
    {
        var cart = new Cart();
        cart.Add(Book, 2);                                       // 70.00
        cart.Add(Mug, 3);                                        // 28.50
        cart.Add(Pen, 1);                                        //  1.50  → exactly 100.00

        Assert.Equal((100m, 10m, 90m), (cart.Subtotal, cart.Discount, cart.Total));
    }

    [Fact]
    public void JustBelowTheThreshold_NoDiscount()
    {
        var cart = new Cart();
        cart.Add(Book, 2);
        cart.Add(Mug, 3);                                        // 98.50

        Assert.Equal(0m, cart.Discount);
    }

    [Fact]
    public void Remove_DropsTheLine_AndReportsWhetherItWasThere()
    {
        var cart = new Cart();
        cart.Add(Pen, 1);

        Assert.True(cart.Remove("pen-1"));
        Assert.False(cart.Remove("PEN-1"));
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void Clear_EmptiesTheCart()
    {
        var cart = new Cart();
        cart.Add(Pen, 1);
        cart.Add(Mug, 1);

        cart.Clear();

        Assert.Equal(0, cart.ItemCount);
    }

    [Fact]
    public void ItemCount_AddsUpTheQuantitiesOfAllLines()
    {
        var cart = new Cart();
        cart.Add(Pen, 3);
        cart.Add(Mug, 2);

        Assert.Equal(5, cart.ItemCount);
    }
}
