using Pakkeshop.Models;

namespace Pakkeshop.Services;

public interface IOpenAIService
{
    Task<PackageData?> ExtractPackageDataAsync(string emailContent);
    Task<string> GenerateElfResponseAsync(PackageData packageData);
}
