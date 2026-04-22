using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Pakkeshop.Configuration;
using Pakkeshop.Models;

namespace Pakkeshop.Services;

public class OpenAIService : IOpenAIService
{
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIService> _logger;
    private readonly AzureOpenAIClient _client;
    private readonly ISeasonalMessageService _seasonalMessageService;

    public OpenAIService(IOptions<OpenAISettings> settings, ILogger<OpenAIService> logger, ISeasonalMessageService seasonalMessageService)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), new AzureKeyCredential(_settings.ApiKey));
        _seasonalMessageService = seasonalMessageService;
    }

    public async Task<PackageData?> ExtractPackageDataAsync(string emailContent)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.DeploymentName);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "Du er en pakke-assistent der udtræker pakkeoplysninger fra emails. " +
                    "Returner KUN valid JSON med følgende struktur: " +
                    "{\"pakkenummer\": \"string\", \"distributør\": \"string (dao/gls/postnord/bring)\", " +
                    "\"pickupCode\": \"string eller null\", \"sidsteAfhentningsDag\": \"ISO date eller null\", " +
                    "\"pakkeshop\": \"string med fuld adresse til pakkeshop eller null\", " +
                    "\"postnordHentekodeUrl\": \"string eller null\"}. " +
                    "Hvis PostNord skriver at hentekoden vises via et link (fx https://l.postnord.com/...), og koden ikke står direkte i mailen, " +
                    "sæt pickupCode til null og sæt postnordHentekodeUrl til den fulde https-URL til l.postnord.com. " +
                    "Hvis hentekoden står i mailen, skal postnordHentekodeUrl være null. " +
                    "Hvis du ikke kan finde alle oplysninger, sæt de manglende felter til null eller tom string."),
                new UserChatMessage($"Udtræk pakkedata fra denne email:\n\n{emailContent}")
            };

            var response = await chatClient.CompleteChatAsync(messages);

            var content = response.Value.Content[0].Text;
            _logger.LogInformation("OpenAI response: {Response}", content);

            var jsonSlice = TryExtractJsonObject(content) ?? content.Trim();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            PackageData? packageData = null;
            try
            {
                packageData = JsonSerializer.Deserialize<PackageData>(jsonSlice, jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Kunne ikke deserialisere pakkedata-JSON fra model");
            }

            var linkFromBody = PostNordLinkMatcher.FirstMatch(emailContent);

            if (packageData is null && linkFromBody is not null)
            {
                packageData = new PackageData
                {
                    Distributør = "postnord",
                    PostnordHentekodeUrl = linkFromBody
                };
            }

            if (packageData is not null
                && string.IsNullOrWhiteSpace(packageData.PostnordHentekodeUrl)
                && linkFromBody is not null)
                packageData.PostnordHentekodeUrl = linkFromBody;

            var hasPostnordLink = !string.IsNullOrWhiteSpace(packageData?.PostnordHentekodeUrl)
                                  || linkFromBody is not null;
            var hasPakkenummer = !string.IsNullOrEmpty(packageData?.Pakkenummer);
            var extractionOk = packageData is not null && (hasPakkenummer || hasPostnordLink);

            if (extractionOk)
            {
                if (string.IsNullOrWhiteSpace(packageData!.Distributør) && hasPostnordLink)
                    packageData.Distributør = "postnord";

                _logger.LogInformation("Successfully extracted package data: {Pakkenummer} from {Distributør}",
                    string.IsNullOrEmpty(packageData.Pakkenummer) ? "(udfyldes via PostNord-link)" : packageData.Pakkenummer,
                    packageData.Distributør);
                return packageData;
            }

            _logger.LogWarning("Failed to extract valid package data from email");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting package data: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<string> GenerateElfResponseAsync(PackageData packageData)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.DeploymentName);

            var pickupInfo = !string.IsNullOrEmpty(packageData.PickupCode)
                ? $"Pickup code: {packageData.PickupCode}"
                : "Ingen pickup code";

            var lastPickupInfo = !string.IsNullOrEmpty(packageData.SidsteAfhentningsDag)
                ? $"Sidste afhentningsdag: {packageData.SidsteAfhentningsDag}"
                : "Ingen sidste afhentningsdag angivet";

            var pakkeshopInfo = !string.IsNullOrEmpty(packageData.Pakkeshop)
                ? $"Pakkeshop: {packageData.Pakkeshop}"
                : "Ingen pakkeshop adresse";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(_seasonalMessageService.GetSystemPrompt()),
                new UserChatMessage(
                    $"Jeg har netop tilføjet denne pakke til listen:\n" +
                    $"Pakkenummer: {packageData.Pakkenummer}\n" +
                    $"Distributør: {packageData.Distributør}\n" +
                    $"{pickupInfo}\n" +
                    $"{lastPickupInfo}\n" +
                    $"{pakkeshopInfo}\n\n" +
                    "Skriv en bekræftelsesbesked der forklarer at pakken nu er tilføjet til oversigten.")
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var elfMessage = response.Value.Content[0].Text;

            _logger.LogInformation("Generated elf response for package {Pakkenummer}", packageData.Pakkenummer);
            return elfMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating elf response: {Message}", ex.Message);
            return _seasonalMessageService.GetFallbackMessage();
        }
    }

    public async Task<string> GenerateNotificationMessageAsync(PackageData packageData, string senderEmail)
    {
        try
        {
            var chatClient = _client.GetChatClient(_settings.DeploymentName);

            var pickupInfo = !string.IsNullOrEmpty(packageData.PickupCode)
                ? $"Pickup code: {packageData.PickupCode}"
                : "Ingen pickup code";

            var lastPickupInfo = !string.IsNullOrEmpty(packageData.SidsteAfhentningsDag)
                ? $"Sidste afhentningsdag: {packageData.SidsteAfhentningsDag}"
                : "Ingen sidste afhentningsdag angivet";

            var pakkeshopInfo = !string.IsNullOrEmpty(packageData.Pakkeshop)
                ? $"Pakkeshop: {packageData.Pakkeshop}"
                : "Ingen pakkeshop adresse";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(_seasonalMessageService.GetSystemPrompt()),
                new UserChatMessage(
                    $"Nogen anden (email: {senderEmail}) har registreret en ny pakke i systemet:\n" +
                    $"Pakkenummer: {packageData.Pakkenummer}\n" +
                    $"Distributør: {packageData.Distributør}\n" +
                    $"{pickupInfo}\n" +
                    $"{lastPickupInfo}\n" +
                    $"{pakkeshopInfo}\n\n" +
                    "Skriv en kort besked der fortæller at en ny pakke er blevet tilføjet af en anden person. " +
                    "Inkluder hvem der registrerede den, og de vigtigste oplysninger om pakken.")
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var notificationMessage = response.Value.Content[0].Text;

            _logger.LogInformation("Generated notification message for package {Pakkenummer}", packageData.Pakkenummer);
            return notificationMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating notification message: {Message}", ex.Message);
            return $"{_seasonalMessageService.GetFallbackMessage()}\n\n" +
                   $"En ny pakke er blevet registreret af {senderEmail}:\n" +
                   $"Pakkenummer: {packageData.Pakkenummer}\n" +
                   $"Distributør: {packageData.Distributør}";
        }
    }

    private static string? TryExtractJsonObject(string text)
    {
        var i = text.IndexOf('{');
        var j = text.LastIndexOf('}');
        if (i < 0 || j <= i)
            return null;
        return text.Substring(i, j - i + 1);
    }
}
