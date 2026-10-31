using System.Collections.Specialized;
using Shop.Model;
using Shop.ViewModels;

namespace Shop.Tests;

/// <summary>The MVVM view model tested with NO view at all: state in, commands in, notifications out.</summary>
public class ViewModelTests
{
    private readonly ShopViewModel _vm = new(new Catalog(), new Cart());
    private readonly List<string> _changed = [];
    private readonly List<NotifyCollectionChangedAction> _collectionChanged = [];

    public ViewModelTests()
    {
        _vm.PropertyChanged += (_, e) => _changed.Add(e.PropertyName!);
        _vm.Lines.CollectionChanged += (_, e) => _collectionChanged.Add(e.Action);
    }

    private ProductViewModel Product(string sku) => _vm.Products.Single(p => p.Sku == sku);

    private int CanExecuteChangedCount(System.Windows.Input.ICommand command, Action act)
    {
        var count = 0;
        command.CanExecuteChanged += (_, _) => count++;
        act();
        return count;
    }

    [Fact]
    public void TheInitialState_ListsProducts_HasNoLines_AndDisablesTheCommandsThatNeedMore()
    {
        Assert.Equal(3, _vm.Products.Count);
        Assert.Empty(_vm.Lines);
        Assert.Equal(("0.00 €", "0.00 €", "0.00 €"), (_vm.SubtotalText, _vm.DiscountText, _vm.TotalText));
        Assert.False(_vm.AddSelectedCommand.CanExecute(null));       // nothing selected
        Assert.False(_vm.ClearCommand.CanExecute(null));             // nothing to clear
    }

    [Fact]
    public void SelectingAProduct_EnablesAdd_AndAnnouncesIt()
    {
        var announced = CanExecuteChangedCount(_vm.AddSelectedCommand, () => _vm.SelectedProduct = Product("PEN-1"));

        Assert.Equal(1, announced);
        Assert.True(_vm.AddSelectedCommand.CanExecute(null));
        Assert.Contains(nameof(ShopViewModel.SelectedProduct), _changed);
    }

    [Fact]
    public void AZeroQuantity_DisablesAdd()
    {
        _vm.SelectedProduct = Product("PEN-1");

        _vm.Quantity = 0;

        Assert.False(_vm.AddSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void SettingTheSameValueAgain_RaisesNothing()
    {
        _vm.Quantity = 3;
        _changed.Clear();

        _vm.Quantity = 3;

        Assert.Empty(_changed);
    }

    [Fact]
    public void Add_UpdatesLinesAndTotals_AndSetsAStatusMessage()
    {
        _vm.SelectedProduct = Product("BOOK-1");
        _vm.Quantity = 2;

        _vm.AddSelectedCommand.Execute(null);

        var line = Assert.Single(_vm.Lines);
        Assert.Equal(("BOOK-1", 2, "70.00 €"), (line.Sku, line.Quantity, line.LineTotalText));
        Assert.Equal(("70.00 €", "0.00 €", "70.00 €"), (_vm.SubtotalText, _vm.DiscountText, _vm.TotalText));
        Assert.Equal("Added 2 x Clean Architecture (book).", _vm.StatusMessage);
    }

    [Fact]
    public void Add_AnnouncesTheChanges_ToWhoeverIsListening_EachPropertyOnce()
    {
        _vm.SelectedProduct = Product("BOOK-1");
        _vm.Quantity = 2;
        _changed.Clear();

        _vm.AddSelectedCommand.Execute(null);

        Assert.Equal(1, _changed.Count(n => n == nameof(ShopViewModel.TotalText)));
        Assert.Equal(1, _changed.Count(n => n == nameof(ShopViewModel.SubtotalText)));
        Assert.Contains(nameof(ShopViewModel.StatusMessage), _changed);
        Assert.Contains(NotifyCollectionChangedAction.Add, _collectionChanged);
    }

    [Fact]
    public void Add_EnablesClear_AndAnnouncesIt()
    {
        _vm.SelectedProduct = Product("PEN-1");

        var announced = CanExecuteChangedCount(_vm.ClearCommand, () => _vm.AddSelectedCommand.Execute(null));

        Assert.True(announced >= 1);
        Assert.True(_vm.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void ARefusedRule_BecomesAStatusMessage_NotAnException_AndChangesNothing()
    {
        _vm.SelectedProduct = Product("PEN-1");
        _vm.Quantity = Cart.MaxPerLine + 1;

        _vm.AddSelectedCommand.Execute(null);               // does not throw

        Assert.Contains("at most", _vm.StatusMessage);
        Assert.Empty(_vm.Lines);
        Assert.Equal("0.00 €", _vm.TotalText);
    }

    [Fact]
    public void TheDiscount_ShowsUpInTheTotals()
    {
        _vm.SelectedProduct = Product("BOOK-1");
        _vm.Quantity = 3;

        _vm.AddSelectedCommand.Execute(null);

        Assert.Equal(("105.00 €", "10.50 €", "94.50 €"), (_vm.SubtotalText, _vm.DiscountText, _vm.TotalText));
    }

    [Fact]
    public void Remove_TakesTheLineOut_UsingTheLineAsParameter()
    {
        _vm.SelectedProduct = Product("PEN-1");
        _vm.AddSelectedCommand.Execute(null);
        var line = _vm.Lines.Single();

        _vm.RemoveLineCommand.Execute(line);

        Assert.Empty(_vm.Lines);
        Assert.Equal("Removed Pen.", _vm.StatusMessage);
    }

    [Fact]
    public void Remove_IsNotAvailable_WithoutALineParameter() =>
        Assert.False(_vm.RemoveLineCommand.CanExecute(null));

    [Fact]
    public void Clear_EmptiesTheCart_AndDisablesItself()
    {
        _vm.SelectedProduct = Product("PEN-1");
        _vm.AddSelectedCommand.Execute(null);

        _vm.ClearCommand.Execute(null);

        Assert.Empty(_vm.Lines);
        Assert.False(_vm.ClearCommand.CanExecute(null));
        Assert.Equal("Cart cleared.", _vm.StatusMessage);
    }

    [Fact]
    public void ExecutingACommandThatIsNotAvailable_DoesNothing()
    {
        // No product selected: the guard inside the command protects the view model even if a View forgets to disable the button.
        _vm.AddSelectedCommand.Execute(null);

        Assert.Empty(_vm.Lines);
        Assert.Equal("", _vm.StatusMessage);
    }

    [Fact]
    public void ADifferentViewModel_OnTheSameModel_SeesTheSameRules()
    {
        // The Model is shared: another screen (or the MVC app) enforces the very same rules.
        var cart = new Cart();
        var first = new ShopViewModel(new Catalog(), cart);
        first.SelectedProduct = first.Products.Single(p => p.Sku == "PEN-1");
        first.Quantity = 8;
        first.AddSelectedCommand.Execute(null);

        var second = new ShopViewModel(new Catalog(), cart);

        Assert.Equal(8, second.Lines.Single().Quantity);
    }
}
