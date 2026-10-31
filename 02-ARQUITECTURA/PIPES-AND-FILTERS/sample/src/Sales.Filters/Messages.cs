using System.Runtime.CompilerServices;

namespace Sales.Filters;

// The schemas that travel through the pipes. Each filter's input type must be the previous filter's output type.

/// <summary>A line of text from the source, with its 1-based number.</summary>
public sealed record SourceLine(int Number, string Text);

/// <summary>A CSV line split into trimmed fields. Nothing is checked yet.</summary>
public sealed record RawSale(int Line, string Id, string Sku, string Quantity, string UnitPrice, string Country);

/// <summary>A sale whose fields are valid and normalized.</summary>
public sealed record Sale(int Line, string Id, string Sku, int Quantity, decimal UnitPrice, string Country);

/// <summary>A sale with its amounts calculated.</summary>
public sealed record PricedSale(
    int Line, string Id, string Sku, int Quantity, string Country, decimal Net, decimal Vat, decimal Total);

public static class LineSource
{
    /// <summary>Reads the reader one line at a time, only when the pipeline asks for the next one.</summary>
    public static async IAsyncEnumerable<SourceLine> FromReader(
        TextReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var number = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } text)
        {
            number++;
            yield return new SourceLine(number, text);
        }
    }

    public static IAsyncEnumerable<SourceLine> FromLines(IEnumerable<string> lines) =>
        lines.Select((text, index) => new SourceLine(index + 1, text)).ToAsyncEnumerable();
}
