namespace Shipping.Contracts;

/// <summary>The version of this contract. The kernel only accepts plugins written for the version it supports.</summary>
public static class ShippingContract
{
    public const int Version = 1;
}

/// <summary>What a customer wants to ship.</summary>
public sealed record Parcel(decimal WeightKg, int DistanceKm);

/// <summary>What a carrier offers for a parcel.</summary>
public sealed record Quote(string CarrierId, decimal Price, int Days);

/// <summary>
/// The extension point. A plugin implements this and nothing else is needed from the kernel's side.
/// Return null when the carrier does not serve that parcel.
/// </summary>
public interface IShippingCarrier
{
    Quote? GetQuote(Parcel parcel);
}

/// <summary>Marks a class as a carrier plugin and declares its id and the contract version it was written for.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CarrierAttribute(string id, int contractVersion = ShippingContract.Version) : Attribute
{
    public string Id { get; } = id;
    public int ContractVersion { get; } = contractVersion;
}
