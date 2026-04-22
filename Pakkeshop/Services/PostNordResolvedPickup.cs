namespace Pakkeshop.Services;

public sealed class PostNordResolvedPickup
{
    public required string ShipmentId { get; init; }
    public required string PickupCode { get; init; }
}
