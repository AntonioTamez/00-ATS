using Shipping.Contracts;

namespace Fixtures.FutureContract;

/// <summary>A plugin written for a contract version the kernel does not know.</summary>
[Carrier("future", 99)]
public sealed class FutureCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => new Quote("future", 1m, 1);
}
