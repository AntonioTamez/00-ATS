using Shipping.Contracts;

namespace Shipping.Kernel;

public sealed record CarrierFailure(string CarrierId, string Reason);

public sealed record QuoteResult(IReadOnlyList<Quote> Quotes, IReadOnlyList<CarrierFailure> Failures)
{
    /// <summary>The cheapest offer (ties: faster, then carrier id), or null when nobody serves the parcel.</summary>
    public Quote? Cheapest => Quotes.Count == 0 ? null : Quotes[0];
}

/// <summary>
/// The one business capability of the core: ask every installed carrier and compare. It protects itself from plugins:
/// a carrier that throws, or answers with something invalid, is reported as a failure and the rest still count.
/// </summary>
/// <remarks>This is fault isolation for honest mistakes, not a security boundary: plugins run in the host process.</remarks>
public sealed class QuoteService(PluginCatalog catalog)
{
    public QuoteResult GetQuotes(Parcel parcel)
    {
        if (parcel.WeightKg <= 0) throw new ArgumentOutOfRangeException(nameof(parcel), "The weight must be positive.");
        if (parcel.DistanceKm < 0) throw new ArgumentOutOfRangeException(nameof(parcel), "The distance cannot be negative.");

        var quotes = new List<Quote>();
        var failures = new List<CarrierFailure>();

        foreach (var carrier in catalog.Carriers)
        {
            try
            {
                var quote = carrier.Instance.GetQuote(parcel);
                if (quote is null) continue;

                if (quote.CarrierId != carrier.Id)
                    failures.Add(new CarrierFailure(carrier.Id, $"answered with a quote signed by '{quote.CarrierId}'"));
                else if (quote.Price <= 0 || quote.Days < 0)
                    failures.Add(new CarrierFailure(carrier.Id, "answered with an invalid price or delivery time"));
                else
                    quotes.Add(quote);
            }
            catch (Exception ex)
            {
                failures.Add(new CarrierFailure(carrier.Id, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        var ordered = quotes.OrderBy(q => q.Price).ThenBy(q => q.Days).ThenBy(q => q.CarrierId, StringComparer.Ordinal).ToList();
        return new QuoteResult(ordered, failures);
    }
}
