using Shipping.Contracts;

namespace Fixtures.Impostor;

/// <summary>A plugin that answers with a quote signed by ANOTHER carrier.</summary>
[Carrier("impostor")]
public sealed class ImpostorCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => new Quote("standard", 1m, 1);
}
