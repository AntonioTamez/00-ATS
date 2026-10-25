using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Shop.Model;

namespace Shop.ViewModels;

public sealed class ProductViewModel(Product product)
{
    public Product Product { get; } = product;
    public string Sku => Product.Sku;
    public string Name => Product.Name;
    public string PriceText => Format.Money(Product.Price);

    public override string ToString() => $"{Name} ({PriceText})";
}

public sealed class CartLineViewModel(CartLine line)
{
    public string Sku => line.Product.Sku;
    public string Name => line.Product.Name;
    public int Quantity => line.Quantity;
    public string UnitPriceText => Format.Money(line.Product.Price);
    public string LineTotalText => Format.Money(line.Total);
}

internal static class Format
{
    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " €";
}

/// <summary>
/// The whole screen as STATE + COMMANDS. It never references a button, a page or a console: the View binds to it,
/// and when anything changes this class announces it (PropertyChanged / CollectionChanged).
/// </summary>
public sealed class ShopViewModel : ObservableObject
{
    private readonly Cart _cart;
    private ProductViewModel? _selected;
    private int _quantity = 1;
    private string _statusMessage = "";
    private string _subtotalText = "";
    private string _discountText = "";
    private string _totalText = "";
    private readonly RelayCommand _add;
    private readonly RelayCommand _remove;
    private readonly RelayCommand _clear;

    public ShopViewModel(Catalog catalog, Cart cart)
    {
        _cart = cart;
        Products = new ObservableCollection<ProductViewModel>(catalog.All.Select(p => new ProductViewModel(p)));
        Lines = [];

        _add = new RelayCommand(_ => AddSelected(), _ => SelectedProduct is not null && Quantity > 0);
        _remove = new RelayCommand(p => RemoveLine((CartLineViewModel)p!), p => p is CartLineViewModel);
        _clear = new RelayCommand(_ => ClearCart(), _ => Lines.Count > 0);

        Refresh();
    }

    public ObservableCollection<ProductViewModel> Products { get; }

    public ObservableCollection<CartLineViewModel> Lines { get; }

    public ProductViewModel? SelectedProduct
    {
        get => _selected;
        set { if (Set(ref _selected, value)) _add.RaiseCanExecuteChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { if (Set(ref _quantity, value)) _add.RaiseCanExecuteChanged(); }
    }

    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }

    public string SubtotalText { get => _subtotalText; private set => Set(ref _subtotalText, value); }

    public string DiscountText { get => _discountText; private set => Set(ref _discountText, value); }

    public string TotalText { get => _totalText; private set => Set(ref _totalText, value); }

    // Exposed as ICommand on purpose: the View must not care which class implements it.
    public ICommand AddSelectedCommand => _add;
    public ICommand RemoveLineCommand => _remove;
    public ICommand ClearCommand => _clear;

    private void AddSelected()
    {
        try
        {
            _cart.Add(SelectedProduct!.Product, Quantity);
            StatusMessage = $"Added {Quantity} x {SelectedProduct.Name}.";
        }
        catch (CartRuleException ex)
        {
            StatusMessage = ex.Message;           // a refused rule becomes state the View shows; it is not an exception for the View
        }
        Refresh();
    }

    private void RemoveLine(CartLineViewModel line)
    {
        _cart.Remove(line.Sku);
        StatusMessage = $"Removed {line.Name}.";
        Refresh();
    }

    private void ClearCart()
    {
        _cart.Clear();
        StatusMessage = "Cart cleared.";
        Refresh();
    }

    /// <summary>Re-reads the Model and publishes what changed. Totals are raised once each, after the work is done.</summary>
    private void Refresh()
    {
        Lines.Clear();
        foreach (var line in _cart.Lines) Lines.Add(new CartLineViewModel(line));

        SubtotalText = Format.Money(_cart.Subtotal);
        DiscountText = Format.Money(_cart.Discount);
        TotalText = Format.Money(_cart.Total);
        _clear.RaiseCanExecuteChanged();
    }
}
