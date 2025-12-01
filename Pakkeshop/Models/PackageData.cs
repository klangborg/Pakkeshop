namespace Pakkeshop.Models;

public class PackageData
{
    public string Pakkenummer { get; set; } = string.Empty;
    public string Distributør { get; set; } = string.Empty;
    public string? PickupCode { get; set; }
    public string? SidsteAfhentningsDag { get; set; }
    public string? Pakkeshop { get; set; }
}
