using System.Collections.Specialized;
using System.ComponentModel;
using Shop.ViewModels;

namespace Shop.ConsoleView;

/// <summary>
/// A "View" for the MVVM pattern, drawn in a console so it can be tested without any UI framework.
/// It does what a XAML binding does: it OBSERVES the view model and redraws when it changes, and it forwards
/// user input to the view model's COMMANDS. It never touches the Model and contains no business rule.
/// </summary>
public sealed class ConsoleShopView
{
    private readonly ShopViewModel _vm;
    private readonly TextWriter _out;
    private bool _dirty;

    public ConsoleShopView(ShopViewModel viewModel, TextWriter output)
    {
        _vm = viewModel;
        _out = output;

        // "Data binding": the view subscribes; the view model has no idea we exist.
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.Lines.CollectionChanged += OnLinesChanged;
    }

    public void Run(TextReader input)
    {
        Render();
        while (input.ReadLine() is { } line)
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) continue;
            if (parts[0] == "quit") break;

            Handle(parts[0], parts.Length > 1 ? parts[1] : "");
            if (_dirty) Render();
        }
    }

    private void Handle(string command, string argument)
    {
        switch (command)
        {
            case "select":
                var product = _vm.Products.FirstOrDefault(p => string.Equals(p.Sku, argument, StringComparison.OrdinalIgnoreCase));
                if (product is null) _out.WriteLine($"! Unknown product '{argument}'.");
                else _vm.SelectedProduct = product;
                break;
            case "qty":
                if (int.TryParse(argument, out var quantity)) _vm.Quantity = quantity;
                else _out.WriteLine("! qty needs a number.");
                break;
            case "add":
                Execute(_vm.AddSelectedCommand, null, "add");
                break;
            case "remove":
                if (int.TryParse(argument, out var position) && position >= 1 && position <= _vm.Lines.Count)
                    Execute(_vm.RemoveLineCommand, _vm.Lines[position - 1], "remove");
                else _out.WriteLine("! remove needs a line number.");
                break;
            case "clear":
                Execute(_vm.ClearCommand, null, "clear");
                break;
            default:
                _out.WriteLine($"! Unknown command '{command}'. Try: select <sku>, qty <n>, add, remove <n>, clear, quit.");
                break;
        }
    }

    /// <summary>A button bound to a command is disabled while CanExecute is false. Here, we say so.</summary>
    private void Execute(System.Windows.Input.ICommand command, object? parameter, string name)
    {
        if (!command.CanExecute(parameter)) _out.WriteLine($"! '{name}' is not available right now.");
        else command.Execute(parameter);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => _dirty = true;

    private void Render()
    {
        _dirty = false;
        _out.WriteLine("=== Shop (MVVM, console view) ===");
        _out.WriteLine("Products:");
        foreach (var p in _vm.Products)
            _out.WriteLine($"  {(ReferenceEquals(p, _vm.SelectedProduct) ? ">" : " ")} {p.Sku,-7} {p.Name} - {p.PriceText}");

        _out.WriteLine($"Quantity: {_vm.Quantity}   [add: {(_vm.AddSelectedCommand.CanExecute(null) ? "enabled" : "disabled")}]");
        _out.WriteLine("Cart:");
        if (_vm.Lines.Count == 0) _out.WriteLine("  (empty)");
        for (var i = 0; i < _vm.Lines.Count; i++)
        {
            var l = _vm.Lines[i];
            _out.WriteLine($"  {i + 1}. {l.Name} x{l.Quantity} = {l.LineTotalText}");
        }
        _out.WriteLine($"Subtotal {_vm.SubtotalText} | Discount {_vm.DiscountText} | TOTAL {_vm.TotalText}");
        if (_vm.StatusMessage.Length > 0) _out.WriteLine($"Status: {_vm.StatusMessage}");
        _out.WriteLine();
    }
}
