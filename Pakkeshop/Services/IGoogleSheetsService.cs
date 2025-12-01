using Pakkeshop.Models;

namespace Pakkeshop.Services;

public interface IGoogleSheetsService
{
    Task AppendRowAsync(PackageData data);
}
