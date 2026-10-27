using Shipping.Contracts;
using Shipping.Kernel;

namespace Shipping.Tests;

/// <summary>The core's one capability, working with whatever plugins are installed.</summary>
public class QuoteTests
{
    private static QuoteResult Quote(Parcel parcel, params string[] plugins)
    {
        using var sandbox = new Sandbox().With(plugins);
        using var catalog = PluginCatalog.Load(sandbox.Root);
        return new QuoteService(catalog).GetQuotes(parcel);
    }

    [Fact]
    public void EveryInstalledCarrierQuotes_AndTheCheapestWins()
    {
        var result = Quote(new Parcel(2m, 100), "Carriers.Standard", "Carriers.Express");

        Assert.Equal([new("standard", 7.00m, 3), new("express", 17.40m, 1)], result.Quotes);
        Assert.Equal("standard", result.Cheapest!.CarrierId);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ACarrierThatDoesNotServeTheParcel_SimplyDoesNotQuote()
    {
        var result = Quote(new Parcel(25m, 100), "Carriers.Standard", "Carriers.Express");   // express stops at 20 kg

        Assert.Equal([new Quote("standard", 18.50m, 3)], result.Quotes);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void WhenNobodyServesTheParcel_ThereIsNoCheapest()
    {
        var result = Quote(new Parcel(40m, 100), "Carriers.Standard", "Carriers.Express");

        Assert.Empty(result.Quotes);
        Assert.Null(result.Cheapest);
    }

    [Fact]
    public void WithoutPlugins_TheKernelRunsAndQuotesNothing()
    {
        var result = Quote(new Parcel(2m, 100));

        Assert.Null(result.Cheapest);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ExpressGetsSlowerBeyond1000Km()
    {
        var result = Quote(new Parcel(2m, 1500), "Carriers.Express");

        Assert.Equal(2, result.Cheapest!.Days);
    }

    [Fact]
    public void ACarrierThatThrows_IsReportedAsAFailure_AndTheOthersStillQuote()
    {
        var result = Quote(new Parcel(2m, 100), "Carriers.Standard", "Fixtures.Faulty");

        Assert.Equal("standard", result.Cheapest!.CarrierId);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("faulty", failure.CarrierId);
        Assert.Contains("boom", failure.Reason);
    }

    [Fact]
    public void AQuoteSignedByAnotherCarrier_IsRefused()
    {
        var result = Quote(new Parcel(2m, 100), "Carriers.Standard", "Fixtures.Impostor");

        Assert.Equal([new Quote("standard", 7.00m, 3)], result.Quotes);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("impostor", failure.CarrierId);
        Assert.Contains("signed by 'standard'", failure.Reason);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(5, -1)]
    public void AnInvalidParcel_IsRefusedByTheKernel_BeforeAnyPluginSeesIt(int weight, int distance)
    {
        using var sandbox = new Sandbox().With("Fixtures.Faulty");
        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Throws<ArgumentOutOfRangeException>(() => new QuoteService(catalog).GetQuotes(new Parcel(weight, distance)));
    }

    [Fact]
    public void TheSamePluginsGiveTheSameAnswer_EveryTime()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard", "Carriers.Express");
        using var catalog = PluginCatalog.Load(sandbox.Root);
        var service = new QuoteService(catalog);

        Assert.Equal(service.GetQuotes(new Parcel(3m, 50)).Quotes, service.GetQuotes(new Parcel(3m, 50)).Quotes);
    }
}
