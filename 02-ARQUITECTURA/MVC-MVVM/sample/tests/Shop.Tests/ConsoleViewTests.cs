using Shop.ConsoleView;
using Shop.Model;
using Shop.ViewModels;

namespace Shop.Tests;

/// <summary>A View, tested by feeding it input and reading what it draws. It holds no logic of its own to test.</summary>
public class ConsoleViewTests
{
    private static string Run(params string[] inputLines)
    {
        var output = new StringWriter();
        var view = new ConsoleShopView(new ShopViewModel(new Catalog(), new Cart()), output);
        view.Run(new StringReader(string.Join(Environment.NewLine, inputLines)));
        return output.ToString();
    }

    [Fact]
    public void TheFirstScreen_ListsProducts_AnEmptyCart_AndADisabledAdd()
    {
        var screen = Run();

        Assert.Contains("Clean Architecture (book) - 35.00 €", screen);
        Assert.Contains("(empty)", screen);
        Assert.Contains("[add: disabled]", screen);
    }

    [Fact]
    public void SelectingAProduct_MarksIt_AndEnablesAdd()
    {
        var screen = Run("select BOOK-1");

        Assert.Contains("> BOOK-1", screen);
        Assert.Contains("[add: enabled]", screen);
    }

    [Fact]
    public void Adding_RedrawsTheCartAndTheTotal_BecauseTheViewObservesTheViewModel()
    {
        var screen = Run("select BOOK-1", "qty 2", "add");

        Assert.Contains("1. Clean Architecture (book) x2 = 70.00 €", screen);
        Assert.Contains("TOTAL 70.00 €", screen);
        Assert.Contains("Status: Added 2 x Clean Architecture (book).", screen);
    }

    [Fact]
    public void TheDiscount_IsShown_WhenTheModelAppliesIt()
    {
        var screen = Run("select BOOK-1", "qty 3", "add");

        Assert.Contains("Discount 10.50 €", screen);
        Assert.Contains("TOTAL 94.50 €", screen);
    }

    [Fact]
    public void AddingWithoutASelection_IsRefused_ByTheCommandNotByTheView()
    {
        var screen = Run("add");

        Assert.Contains("'add' is not available right now.", screen);
        Assert.Contains("(empty)", screen);
    }

    [Fact]
    public void ARuleViolation_ShowsAsStatus()
    {
        var screen = Run("select PEN-1", "qty 11", "add");

        Assert.Contains("Status: You can have at most 10 units of Pen.", screen);
    }

    [Fact]
    public void RemovingALine_ByItsNumber_Works()
    {
        var screen = Run("select PEN-1", "add", "remove 1");

        Assert.Contains("Status: Removed Pen.", screen);
        Assert.EndsWith($"TOTAL 0.00 €{Environment.NewLine}Status: Removed Pen.{Environment.NewLine}{Environment.NewLine}", screen);
    }

    [Fact]
    public void ClearingAnEmptyCart_IsNotAvailable() =>
        Assert.Contains("'clear' is not available right now.", Run("clear"));

    [Fact]
    public void BadInput_GetsAHelpfulMessage_AndDoesNotBreakAnything()
    {
        var screen = Run("select NOPE", "qty abc", "remove 9", "dance");

        Assert.Contains("! Unknown product 'NOPE'.", screen);
        Assert.Contains("! qty needs a number.", screen);
        Assert.Contains("! remove needs a line number.", screen);
        Assert.Contains("! Unknown command 'dance'.", screen);
    }

    [Fact]
    public void Quit_StopsProcessingTheRest()
    {
        var screen = Run("quit", "select BOOK-1", "add");

        Assert.DoesNotContain("Status: Added", screen);
    }
}
