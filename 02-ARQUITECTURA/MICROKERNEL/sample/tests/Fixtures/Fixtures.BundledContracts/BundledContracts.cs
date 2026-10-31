using Shipping.Contracts;

namespace Fixtures.BundledContracts;

/// <summary>A well-behaved plugin whose build COPIED Shipping.Contracts.dll next to itself (the classic mistake).</summary>
[Carrier("bundled")]
public sealed class BundledCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => new Quote("bundled", 9.99m, 4);
}
