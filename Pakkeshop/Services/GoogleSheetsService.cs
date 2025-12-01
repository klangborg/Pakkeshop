using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pakkeshop.Configuration;
using Pakkeshop.Models;

namespace Pakkeshop.Services;

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly GoogleSheetsSettings _settings;
    private readonly ILogger<GoogleSheetsService> _logger;
    private readonly SheetsService _sheetsService;

    public GoogleSheetsService(IOptions<GoogleSheetsSettings> settings, ILogger<GoogleSheetsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var credential = GoogleCredential.FromJson(_settings.CredentialsJson)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Pakkeshop"
        });
    }

    public async Task AppendRowAsync(PackageData data)
    {
        const int maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var range = $"{_settings.SheetName}!A:E";
                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>>
                    {
                        new List<object>
                        {
                            data.Pakkenummer,
                            data.Distributør,
                            data.PickupCode ?? string.Empty,
                            data.SidsteAfhentningsDag ?? string.Empty,
                            data.Pakkeshop ?? string.Empty
                        }
                    }
                };

                var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, _settings.SpreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

                var response = await appendRequest.ExecuteAsync();

                _logger.LogInformation("Successfully appended package {Pakkenummer} to Google Sheets at {Range}",
                    data.Pakkenummer, response.Updates.UpdatedRange);

                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Attempt {Retry} failed to append to Google Sheets: {Message}",
                    retryCount, ex.Message);

                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to append to Google Sheets after {MaxRetries} attempts", maxRetries);
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
            }
        }
    }
}
