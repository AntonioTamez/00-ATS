using Sales.Filters;
using Sales.Pipeline;
using static Sales.Tests.Support;

namespace Sales.Tests;

/// <summary>Each domain filter on its own: no other filter, no composition.</summary>
public class FilterTests
{
    // ---------- parse-csv ----------

    [Fact]
    public async Task ParseCsv_SkipsTheHeaderAndBlankLines_AndTrimsFields()
    {
        var (output, rejected) = await RunAlone(new ParseCsvFilter(),
            new SourceLine(1, "ID,SKU,QTY,PRICE,COUNTRY"),
            new SourceLine(2, " 1 , abc , 2 , 3.50 , ar "),
            new SourceLine(3, "   "));

        Assert.Equal(new RawSale(2, "1", "abc", "2", "3.50", "ar"), Assert.Single(output));
        Assert.Empty(rejected);
    }

    [Fact]
    public async Task ParseCsv_OnlyTheFirstLineCanBeTheHeader()
    {
        var (output, _) = await RunAlone(new ParseCsvFilter(), new SourceLine(2, "id,a,b,c,d"));

        Assert.Equal("id", Assert.Single(output).Id);
    }

    [Theory]
    [InlineData("1,abc,2", 3)]
    [InlineData("1,abc,2,3.50,AR,extra", 6)]
    public async Task ParseCsv_RefusesLinesWithTheWrongNumberOfColumns(string text, int found)
    {
        var (output, rejected) = await RunAlone(new ParseCsvFilter(), new SourceLine(5, text));

        Assert.Empty(output);
        Assert.Equal(new Rejection("parse-csv", "line 5", $"expected 5 columns, found {found}"), Assert.Single(rejected));
    }

    // ---------- validate-sale ----------

    [Fact]
    public async Task Validate_TurnsTextIntoTypedValues_AndNormalizesSkuAndCountry()
    {
        var (output, rejected) = await RunAlone(new ValidateSaleFilter(), new RawSale(2, "1", "abc-1", "3", "2.50", "ar"));

        Assert.Equal(new Sale(2, "1", "ABC-1", 3, 2.50m, "AR"), Assert.Single(output));
        Assert.Empty(rejected);
    }

    [Fact]
    public async Task Validate_AcceptsAFreeItem() // price 0 is allowed; negative is not
    {
        var (output, _) = await RunAlone(new ValidateSaleFilter(), new RawSale(2, "1", "abc", "1", "0", "AR"));

        Assert.Equal(0m, Assert.Single(output).UnitPrice);
    }

    [Theory]
    [InlineData("", "abc", "1", "1.00", "AR", "empty id")]
    [InlineData("1", "", "1", "1.00", "AR", "empty sku")]
    [InlineData("1", "abc", "x", "1.00", "AR", "quantity 'x' is not a positive integer")]
    [InlineData("1", "abc", "0", "1.00", "AR", "quantity '0' is not a positive integer")]
    [InlineData("1", "abc", "-2", "1.00", "AR", "quantity '-2' is not a positive integer")]
    [InlineData("1", "abc", "1.5", "1.00", "AR", "quantity '1.5' is not a positive integer")]
    [InlineData("1", "abc", "1", "abc", "AR", "unit price 'abc' is not a non-negative number")]
    [InlineData("1", "abc", "1", "-1", "AR", "unit price '-1' is not a non-negative number")]
    [InlineData("1", "abc", "1", "1.00", "ARG", "country 'ARG' must be 2 letters")]
    [InlineData("1", "abc", "1", "1.00", "A1", "country 'A1' must be 2 letters")]
    [InlineData("1", "abc", "1", "1.00", "", "country '' must be 2 letters")]
    public async Task Validate_RefusesEachBadField_WithTheReasonForThatField(
        string id, string sku, string qty, string price, string country, string reason)
    {
        var (output, rejected) = await RunAlone(new ValidateSaleFilter(), new RawSale(7, id, sku, qty, price, country));

        Assert.Empty(output);
        Assert.Equal(new Rejection("validate-sale", "line 7", reason), Assert.Single(rejected));
    }

    // ---------- deduplicate ----------

    [Fact]
    public async Task Deduplicate_KeepsTheFirstSaleOfAnId_AndRefusesTheRepeats()
    {
        var (output, rejected) = await RunAlone(new DeduplicateFilter(),
            new Sale(2, "A", "X", 1, 1m, "AR"),
            new Sale(3, "B", "X", 1, 1m, "AR"),
            new Sale(4, "A", "Y", 9, 9m, "ES"));

        Assert.Equal([2, 3], output.Select(s => s.Line));
        Assert.Equal(new Rejection("deduplicate", "line 4", "duplicate id 'A' (first seen at line 2)"), Assert.Single(rejected));
    }

    [Fact]
    public async Task Deduplicate_RemembersNothingBetweenRuns()
    {
        var filter = new DeduplicateFilter();
        var sale = new Sale(2, "A", "X", 1, 1m, "AR");

        var first = await RunAlone(filter, sale);
        var second = await RunAlone(filter, sale);

        Assert.Single(first.Output);
        Assert.Single(second.Output);
        Assert.Empty(second.Rejected);
    }

    // ---------- blocked-skus ----------

    [Fact]
    public async Task BlockedSkus_RefusesBlockedSkus_IgnoringCase()
    {
        var (output, rejected) = await RunAlone(new BlockedSkusFilter([" ban-001 "]),
            new Sale(2, "1", "BAN-001", 1, 1m, "AR"),
            new Sale(3, "2", "OK-1", 1, 1m, "AR"));

        Assert.Equal("OK-1", Assert.Single(output).Sku);
        Assert.Equal(new Rejection("blocked-skus", "line 2", "sku 'BAN-001' is blocked"), Assert.Single(rejected));
    }

    // ---------- apply-vat ----------

    private static ApplyVatFilter Vat() => new(new Dictionary<string, decimal> { ["AR"] = 0.21m, ["CL"] = 0.19m });

    [Fact]
    public async Task Vat_CalculatesNetVatAndTotal_PerCountryRate()
    {
        var (output, rejected) = await RunAlone(Vat(), new Sale(2, "1", "X", 2, 10m, "AR"));

        var priced = Assert.Single(output);
        Assert.Equal((20.00m, 4.20m, 24.20m), (priced.Net, priced.Vat, priced.Total));
        Assert.Empty(rejected);
    }

    [Fact]
    public async Task Vat_RoundsHalvesAwayFromZero()
    {
        // net 11.50 * 19% = 2.185 -> 2.19 (banker's rounding would give 2.18)
        var (output, _) = await RunAlone(Vat(), new Sale(2, "1", "X", 10, 1.15m, "CL"));

        var priced = Assert.Single(output);
        Assert.Equal((11.50m, 2.19m, 13.69m), (priced.Net, priced.Vat, priced.Total));
    }

    [Fact]
    public async Task Vat_RefusesACountryWithoutARate()
    {
        var (output, rejected) = await RunAlone(Vat(), new Sale(9, "1", "X", 1, 1m, "XX"));

        Assert.Empty(output);
        Assert.Equal(new Rejection("apply-vat", "line 9", "no VAT rate for country 'XX'"), Assert.Single(rejected));
    }

    [Fact]
    public async Task Vat_AFreeItemHasNoVat()
    {
        var (output, _) = await RunAlone(Vat(), new Sale(2, "1", "X", 3, 0m, "AR"));

        Assert.Equal((0m, 0m, 0m), (output[0].Net, output[0].Vat, output[0].Total));
    }

    // ---------- source ----------

    [Fact]
    public async Task LineSource_NumbersLinesFromOne()
    {
        var lines = await LineSource.FromReader(new StringReader("a\nb\n")).ToListAsync();

        Assert.Equal([new SourceLine(1, "a"), new SourceLine(2, "b")], lines);
    }
}
