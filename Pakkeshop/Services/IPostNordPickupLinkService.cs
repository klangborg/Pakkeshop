namespace Pakkeshop.Services;

public interface IPostNordPickupLinkService
{
    /// <summary>
    /// Følger PostNords korte link og læser hentekode + forsendelses-id fra redirect (pickupcode.aws.postnord.com).
    /// </summary>
    Task<PostNordResolvedPickup?> ResolveFromShortLinkAsync(string candidateUrl, CancellationToken cancellationToken = default);
}
