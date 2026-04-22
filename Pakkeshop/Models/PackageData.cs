namespace Pakkeshop.Models;

public class PackageData
{
    public string Pakkenummer { get; set; } = string.Empty;
    public string Distributør { get; set; } = string.Empty;
    public string? PickupCode { get; set; }
    public string? SidsteAfhentningsDag { get; set; }
    public string? Pakkeshop { get; set; }

    /// <summary>
    /// PostNord kort-link (l.postnord.com/...) når hentekode ikke står i mailen. Bruges kun under behandling.
    /// </summary>
    public string? PostnordHentekodeUrl { get; set; }
}
