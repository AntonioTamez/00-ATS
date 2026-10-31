using Sales.Filters;
using Sales.Host;
using Sales.Pipeline;
using static Sales.Tests.Support;

namespace Sales.Tests;

/// <summary>Filters composed into real pipelines, and the command line on top.</summary>
public class CompositionTests
{
    private static IAsyncEnumerable<SourceLine> SampleLines() => LineSource.FromLines(File.ReadLines(SampleCsv));

    private static async Task<(List<PricedSale> Sales, IReadOnlyList<Rejection> Rejected)> Run(Pipeline<SourceLine, PricedSale> pipeline)
    {
        var run = pipeline.Start(SampleLines());
        return (await run.Output.ToListAsync(), run.Rejections);
    }

    [Fact]
    public async Task TheStandardPipeline_OnTheSampleFile_AcceptsFourSales_AndExplainsEveryRejection()
    {
        var (sales, rejected) = await Run(SalesPipelines.Standard());

        Assert.Equal([2, 3, 6, 12], sales.Select(s => s.Line));
        Assert.Equal((132.90m, 27.70m, 160.60m), (sales.Sum(s => s.Net), sales.Sum(s => s.Vat), sales.Sum(s => s.Total)));
        Assert.Equal(
            [
                new Rejection("blocked-skus", "line 4", "sku 'BAN-001' is blocked"),
                new Rejection("validate-sale", "line 5", "quantity 'x' is not a positive integer"),
                new Rejection("deduplicate", "line 7", "duplicate id '1001' (first seen at line 2)"),
                new Rejection("apply-vat", "line 8", "no VAT rate for country 'XX'"),
                new Rejection("parse-csv", "line 9", "expected 5 columns, found 3"),
                new Rejection("validate-sale", "line 10", "quantity '-2' is not a positive integer"),
            ],
            rejected);
    }

    [Fact]
    public async Task TheQuickPipeline_ReusesTheSameFilters_WithoutDedupAndBlocking()
    {
        var (sales, rejected) = await Run(SalesPipelines.Quick());

        Assert.Equal([2, 3, 4, 6, 7, 12], sales.Select(s => s.Line));
        Assert.Equal(
            ["validate-sale", "apply-vat", "parse-csv", "validate-sale"],
            rejected.OrderBy(r => int.Parse(r.Item[5..])).Select(r => r.Filter));
    }

    [Fact]
    public void ThePipelinesShareFiltersButNotTheirOrder()
    {
        Assert.Equal(["parse-csv", "validate-sale", "deduplicate", "blocked-skus", "apply-vat"], SalesPipelines.Standard().Stages);
        Assert.Equal(["parse-csv", "validate-sale", "apply-vat"], SalesPipelines.Quick().Stages);
        Assert.Equal(["parse-csv", "buffer(8)", "validate-sale", "deduplicate", "blocked-skus", "apply-vat"], SalesPipelines.Standard(8).Stages);
    }

    [Fact]
    public async Task ReorderingTwoFiltersThatShareASchema_GivesTheSameResult()
    {
        var blockedFirst = Pipelines.Of<SourceLine>()
            .Then(new ParseCsvFilter())
            .Then(new ValidateSaleFilter())
            .Then(new BlockedSkusFilter(SalesPipelines.BlockedSkus))
            .Then(new DeduplicateFilter())
            .Then(new ApplyVatFilter(SalesPipelines.VatRates));

        var standard = await Run(SalesPipelines.Standard());
        var reordered = await Run(blockedFirst);

        Assert.Equal(standard.Sales, reordered.Sales);
        Assert.Equal(standard.Rejected, reordered.Rejected);
    }

    [Fact]
    public async Task RunningTheSamePipelineTwice_GivesTheSameResult()
    {
        var pipeline = SalesPipelines.Standard();

        var first = await Run(pipeline);
        var second = await Run(pipeline);

        Assert.Equal(first.Sales, second.Sales);
        Assert.Equal(first.Rejected, second.Rejected);
    }

    [Fact]
    public async Task ABufferedPipeline_ProducesTheSameSalesAndTheSameRejections_ThoughNotNecessarilyInTheSameOrder()
    {
        var plain = await Run(SalesPipelines.Standard());
        var buffered = await Run(SalesPipelines.Standard(buffer: 2));

        Assert.Equal(plain.Sales, buffered.Sales);
        Assert.Equal(plain.Rejected.OrderBy(r => r.Item, StringComparer.Ordinal), buffered.Rejected.OrderBy(r => r.Item, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AHugeFile_FlowsThroughWithoutBeingLoadedFirst()
    {
        const int count = 20_000;
        var consumed = 0;
        var maxAhead = 0;
        var produced = 0;

        async IAsyncEnumerable<SourceLine> Lines()
        {
            for (var i = 1; i <= count; i++)
            {
                produced++;
                maxAhead = Math.Max(maxAhead, produced - consumed);
                await Task.Yield();
                yield return new SourceLine(i, $"{i},abc-{i % 7},1,1.00,AR");
            }
        }

        await foreach (var _ in SalesPipelines.Standard().Start(Lines()).Output)
            consumed++;

        Assert.Equal(count, consumed);
        Assert.Equal(1, maxAhead);
    }

    // ---------- command line ----------

    private static async Task<(int Code, string Out, string Err)> Invoke(string[] args, string stdin = "")
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await Cli.RunAsync(args, new StringReader(stdin), stdout, stderr);
        return (code, stdout.ToString().ReplaceLineEndings("\n"), stderr.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Cli_OnTheSampleFile_PrintsTheSalesTheRejectionsAndTheTotals_AndExitsWithOne()
    {
        var (code, output, error) = await Invoke([SampleCsv]);

        Assert.Equal(1, code);
        Assert.Equal("", error);
        Assert.Contains("Pipeline: parse-csv -> validate-sale -> deduplicate -> blocked-skus -> apply-vat\n", output);
        Assert.Contains("  line  3  1002  ABC-200  x1   ES  net    99.90  vat   20.98  total   120.88\n", output);
        Assert.Contains("Accepted: 4  Rejected: 6\n", output);
        Assert.Contains("  [deduplicate] line 7: duplicate id '1001' (first seen at line 2)\n", output);
        Assert.Contains("Totals: net 132.90  vat 27.70  total 160.60\n", output);
    }

    [Fact]
    public async Task Cli_ReadsStandardInputWhenTheFileIsADash_AndExitsWithZeroWhenNothingIsRejected()
    {
        var (code, output, _) = await Invoke(["-", "--quick"], "id,sku,qty,price,country\n1,abc,1,10.00,AR\n");

        Assert.Equal(0, code);
        Assert.Contains("Accepted: 1  Rejected: 0\n", output);
        Assert.Contains("Pipeline: parse-csv -> validate-sale -> apply-vat\n", output);
    }

    [Fact]
    public async Task Cli_TheBufferOptionAddsABufferedPipe()
    {
        var (code, output, _) = await Invoke(["-", "--buffer", "4"], "1,abc,1,10.00,AR\n");

        Assert.Equal(0, code);
        Assert.Contains("parse-csv -> buffer(4) -> validate-sale", output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--quick")]
    [InlineData("a.csv b.csv")]
    [InlineData("- --buffer")]
    [InlineData("- --buffer 0")]
    [InlineData("- --buffer x")]
    [InlineData("- --nope")]
    public async Task Cli_BadUsage_ExitsWithTwoAndPrintsTheUsage(string arguments)
    {
        var (code, output, error) = await Invoke(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Equal("usage: Sales.Host <file|-> [--quick] [--buffer N]\n", error);
    }

    [Fact]
    public async Task Cli_AMissingFile_ExitsWithTwo()
    {
        var (code, _, error) = await Invoke([Path.Combine(SolutionRoot, "data", "nope.csv")]);

        Assert.Equal(2, code);
        Assert.StartsWith("file not found: ", error);
    }

    [Fact]
    public async Task Cli_WhenAFilterFails_ExitsWithThree_NamingTheFilter()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = await Cli.RunAsync(["-"], new ExplodingReader(), stdout, stderr);

        Assert.Equal(3, code);
        Assert.Contains("pipeline failed: filter 'parse-csv' failed: disk on fire", stderr.ToString());
    }

    private sealed class ExplodingReader : TextReader
    {
        public override string ReadLine() => throw new IOException("disk on fire");
    }
}
