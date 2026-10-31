using Shipping.Contracts;

namespace Carriers.Standard;

/// <summary>Cheap and slow. Serves parcels up to 30 kg.</summary>
[Carrier("standard")]
public sealed class StandardCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) =>
        parcel.WeightKg > 30m
            ? null
            : new Quote("standard", Math.Round(5m + 0.5m * parcel.WeightKg + 0.01m * parcel.DistanceKm, 2), 3 + parcel.DistanceKm / 500);
}
