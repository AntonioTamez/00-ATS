using Shipping.Contracts;

namespace Fixtures.Faulty;

/// <summary>A plugin that blows up when asked for a quote.</summary>
[Carrier("faulty")]
public sealed class FaultyCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) => throw new InvalidOperationException("boom");
}
