using Shop.ConsoleView;
using Shop.Model;
using Shop.ViewModels;

// Composition root of the console app: the one place that builds the Model, the ViewModel and the View.
var viewModel = new ShopViewModel(new Catalog(), new Cart());
new ConsoleShopView(viewModel, Console.Out).Run(Console.In);
