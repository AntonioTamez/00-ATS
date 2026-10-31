using System.Runtime.CompilerServices;
using Sales.Pipeline;

namespace Sales.Filters;

/// <summary>Splits CSV lines into fields. Skips the header (line 1) and blank lines.</summary>
public sealed class ParseCsvFilter : IFilter<SourceLine, RawSale>
{
    public const int Columns = 5;

    public string Name => "parse-csv";

    public async IAsyncEnumerable<RawSale> Run(
        IAsyncEnumerable<SourceLine> input, IRejectSink rejects, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in input.WithCancellation(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var fields = line.Text.Split(',');
            if (line.Number == 1 && fields[0].Trim().Equals("id", StringComparison.OrdinalIgnoreCase))
                continue;

            if (fields.Length != Columns)
            {
                rejects.Reject($"line {line.Number}", $"expected {Columns} columns, found {fields.Length}");
                continue;
            }

            yield return new RawSale(
                line.Number, fields[0].Trim(), fields[1].Trim(), fields[2].Trim(), fields[3].Trim(), fields[4].Trim());
        }
    }
}
