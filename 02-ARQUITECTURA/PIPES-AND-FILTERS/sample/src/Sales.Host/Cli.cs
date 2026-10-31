using System.Globalization;
using Sales.Filters;
using Sales.Pipeline;

namespace Sales.Host;

/// <summary>
/// <c>Sales.Host &lt;file|-&gt; [--quick] [--buffer N]</c>. Exit codes: 0 all lines accepted, 1 some lines rejected,
/// 2 bad usage, 3 the pipeline failed.
/// </summary>
public static class Cli
{
    private const string Usage = "usage: Sales.Host <file|-> [--quick] [--buffer N]";

    public static async Task<int> RunAsync(
        string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken = default)
    {
        if (!TryParse(args, out var path, out var quick, out var buffer))
        {
            await stderr.WriteLineAsync(Usage);
            return 2;
        }

        if (path != "-" && !File.Exists(path))
        {
            await stderr.WriteLineAsync($"file not found: {path}");
            return 2;
        }

        var pipeline = quick ? SalesPipelines.Quick(buffer) : SalesPipelines.Standard(buffer);
        await stdout.WriteLineAsync($"Pipeline: {string.Join(" -> ", pipeline.Stages)}");

        using var file = path == "-" ? null : new StreamReader(path);
        var run = pipeline.Start(LineSource.FromReader(file ?? stdin, cancellationToken), cancellationToken);

        decimal net = 0, vat = 0, total = 0;
        var accepted = 0;
        try
        {
            await foreach (var sale in run.Output.WithCancellation(cancellationToken))
            {
                accepted++;
                net += sale.Net;
                vat += sale.Vat;
                total += sale.Total;
                await stdout.WriteLineAsync(Format(
                    $"  line {sale.Line,2}  {sale.Id,-5} {sale.Sku,-8} x{sale.Quantity,-3} {sale.Country}  net {sale.Net,8:0.00}  vat {sale.Vat,7:0.00}  total {sale.Total,8:0.00}"));
            }
        }
        catch (PipelineException ex)
        {
            await stderr.WriteLineAsync($"pipeline failed: {ex.Message}");
            return 3;
        }

        var rejections = run.Rejections;
        await stdout.WriteLineAsync($"Accepted: {accepted}  Rejected: {rejections.Count}");
        foreach (var rejection in rejections)
            await stdout.WriteLineAsync($"  [{rejection.Filter}] {rejection.Item}: {rejection.Reason}");
        await stdout.WriteLineAsync(Format($"Totals: net {net:0.00}  vat {vat:0.00}  total {total:0.00}"));

        return rejections.Count == 0 ? 0 : 1;
    }

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static bool TryParse(string[] args, out string path, out bool quick, out int buffer)
    {
        path = "";
        quick = false;
        buffer = 0;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--quick":
                    quick = true;
                    break;
                case "--buffer":
                    if (i + 1 >= args.Length
                        || !int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out buffer)
                        || buffer < 1)
                        return false;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal) || path.Length > 0)
                        return false;
                    path = args[i];
                    break;
            }
        }

        return path.Length > 0;
    }
}
