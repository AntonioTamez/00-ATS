using Shipping.Contracts;

namespace Carriers.Express;

/// <summary>Fast and pricey. Serves parcels up to 20 kg; one day up to 1000 km, two days beyond.</summary>
[Carrier("express")]
public sealed class ExpressCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel parcel) =>
        parcel.WeightKg > 20m
            ? null
            : new Quote("express", Math.Round(12m + 1.2m * parcel.WeightKg + 0.03m * parcel.DistanceKm, 2), parcel.DistanceKm <= 1000 ? 1 : 2);
}
