using Shipping.Contracts;

namespace Fixtures.StandardClone;

/// <summary>A different plugin that claims the id "standard".</summary>
[Carrier("standard")]
public sealed class StandardCloneCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => new Quote("standard", 0.5m, 1);
}
