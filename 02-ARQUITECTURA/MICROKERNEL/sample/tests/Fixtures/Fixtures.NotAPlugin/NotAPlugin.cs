using Shipping.Contracts;

namespace Fixtures.NotAPlugin;

/// <summary>Implements the interface but forgot the [Carrier] attribute.</summary>
public sealed class ForgottenCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => new Quote("forgotten", 1m, 1);
}
