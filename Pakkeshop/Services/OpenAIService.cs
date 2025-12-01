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

    public OpenAIService(IOptions<OpenAISettings> settings, ILogger<OpenAIService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), new AzureKeyCredential(_settings.ApiKey));
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
                    "\"pakkeshop\": \"string med fuld adresse til pakkeshop eller null\"}. " +
                    "Hvis du ikke kan finde alle oplysninger, sæt de manglende felter til null eller tom string."),
                new UserChatMessage($"Udtræk pakkedata fra denne email:\n\n{emailContent}")
            };

            var response = await chatClient.CompleteChatAsync(messages);

            var content = response.Value.Content[0].Text;
            _logger.LogInformation("OpenAI response: {Response}", content);

            var packageData = JsonSerializer.Deserialize<PackageData>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (packageData != null && !string.IsNullOrEmpty(packageData.Pakkenummer))
            {
                _logger.LogInformation("Successfully extracted package data: {Pakkenummer} from {Distributør}",
                    packageData.Pakkenummer, packageData.Distributør);
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
                new SystemChatMessage(
                    "Du er en hjælpsom julenisse der arbejder med at holde styr på pakker. " +
                    "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                    "så modtageren har overblik over de pakker der kan hentes. " +
                    "Brug et muntert og hyggeligt tone. Inkluder de vigtigste oplysninger om pakken. " +
                    "Underskrive med en hyggelig nissehilsen."),
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
            return "Din pakke er blevet registreret! Ho ho ho! 🎅";
        }
    }
}
