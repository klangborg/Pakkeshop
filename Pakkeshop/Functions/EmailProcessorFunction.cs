using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pakkeshop.Configuration;
using Pakkeshop.Services;

namespace Pakkeshop.Functions;

public class EmailProcessorFunction
{
    private readonly ILogger<EmailProcessorFunction> _logger;
    private readonly IEmailService _emailService;
    private readonly IOpenAIService _openAIService;
    private readonly IGoogleSheetsService _sheetsService;
    private readonly ISeasonalMessageService _seasonalMessageService;
    private readonly IPostNordPickupLinkService _postNordPickupLinkService;
    private readonly EmailSettings _emailSettings;

    public EmailProcessorFunction(
        ILogger<EmailProcessorFunction> logger,
        IEmailService emailService,
        IOpenAIService openAIService,
        IGoogleSheetsService sheetsService,
        ISeasonalMessageService seasonalMessageService,
        IPostNordPickupLinkService postNordPickupLinkService,
        IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailService = emailService;
        _openAIService = openAIService;
        _sheetsService = sheetsService;
        _seasonalMessageService = seasonalMessageService;
        _postNordPickupLinkService = postNordPickupLinkService;
        _emailSettings = emailSettings.Value;
    }

    [Function("EmailProcessor")]
    public async Task Run([TimerTrigger("0 */15 * * * *", RunOnStartup=true)] TimerInfo myTimer)
    {
        _logger.LogInformation("Email processor started at: {Time}", DateTime.UtcNow);

        try
        {
            var emails = await _emailService.GetUnreadEmailsAsync();
            var emailList = emails.ToList();

            if (!emailList.Any())
            {
                _logger.LogInformation("No unread emails found");
                return;
            }

            var successCount = 0;
            var failureCount = 0;

            foreach (var email in emailList)
            {
                var senderEmail = ExtractEmailAddress(email.From);

                try
                {
                    _logger.LogInformation("Processing email from {From} with subject: {Subject}",
                        email.From, email.Subject);

                    var packageData = await _openAIService.ExtractPackageDataAsync(email.Body);

                    string? postnordResolveError = null;
                    if (packageData != null)
                    {
                        var postnordUrl = packageData.PostnordHentekodeUrl ?? PostNordLinkMatcher.FirstMatch(email.Body);
                        if (!string.IsNullOrWhiteSpace(postnordUrl))
                        {
                            var resolved = await _postNordPickupLinkService.ResolveFromShortLinkAsync(postnordUrl);
                            if (resolved != null)
                            {
                                packageData.Pakkenummer = resolved.ShipmentId;
                                packageData.PickupCode = resolved.PickupCode;
                            }
                            else
                            {
                                _logger.LogWarning("Kunne ikke hente PostNord-data fra link for email {UniqueId}", email.UniqueId);
                                packageData = null;
                                postnordResolveError =
                                    "Kunne ikke hente hentekode fra PostNord-linket (linket kan være udløbet eller ugyldigt). " +
                                    "Prøv med et nyt link fra PostNord, eller send pakkenummer og hentekode direkte i mailen.";
                            }
                        }

                        if (packageData != null)
                            packageData.PostnordHentekodeUrl = null;
                    }

                    if (packageData != null)
                    {
                        await _sheetsService.AppendRowAsync(packageData);

                        // Send success email with seasonal message
                        try
                        {
                            var elfMessage = await _openAIService.GenerateElfResponseAsync(packageData);
                            await _emailService.SendEmailAsync(
                                senderEmail,
                                _seasonalMessageService.GetEmailSubject(),
                                elfMessage);
                            _logger.LogInformation("Sent success email to {Sender}", senderEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "Failed to send success email to {Sender}", senderEmail);
                        }

                        // Send notification email if configured and sender not excluded
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(_emailSettings.NotificationEmail) &&
                                !IsSenderExcluded(senderEmail, _emailSettings.ExcludedSenders))
                            {
                                var notificationMessage = await _openAIService.GenerateNotificationMessageAsync(
                                    packageData,
                                    senderEmail);

                                await _emailService.SendEmailAsync(
                                    _emailSettings.NotificationEmail,
                                    $"Ny pakke registreret {_seasonalMessageService.GetCharacterEmoji()}",
                                    notificationMessage);

                                _logger.LogInformation(
                                    "Sent notification email to {NotificationEmail} for package from {Sender}",
                                    _emailSettings.NotificationEmail, senderEmail);
                            }
                            else if (!string.IsNullOrWhiteSpace(_emailSettings.NotificationEmail))
                            {
                                _logger.LogInformation(
                                    "Skipping notification email - sender {Sender} is in excluded list",
                                    senderEmail);
                            }
                        }
                        catch (Exception notificationEx)
                        {
                            // Don't fail the entire process if notification fails
                            _logger.LogWarning(notificationEx,
                                "Failed to send notification email to {NotificationEmail}",
                                _emailSettings.NotificationEmail);
                        }

                        await _emailService.DeleteEmailAsync(email.UniqueId);

                        successCount++;
                        _logger.LogInformation("Successfully processed email {UniqueId}", email.UniqueId);
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning("Failed to extract package data from email {UniqueId}", email.UniqueId);

                        // Send error email
                        await SendErrorEmailAsync(senderEmail, email.Subject,
                            postnordResolveError ??
                            "Kunne ikke udtrække pakkedata fra din email. " +
                            "Kontroller venligst at emailen indeholder alle nødvendige oplysninger: " +
                            "pakkenummer, distributør (DAO/GLS/PostNord/Bring), og evt. pickup code og sidste afhentningsdag.");

                        // Delete email to prevent reprocessing
                        await _emailService.DeleteEmailAsync(email.UniqueId);
                        _logger.LogInformation("Deleted failed email {UniqueId}", email.UniqueId);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Error processing email {UniqueId}: {Message}",
                        email.UniqueId, ex.Message);

                    // Send detailed error email
                    await SendErrorEmailAsync(senderEmail, email.Subject,
                        $"Der opstod en fejl ved behandling af din email:\n\n" +
                        $"Fejltype: {ex.GetType().Name}\n" +
                        $"Fejlbesked: {ex.Message}\n\n" +
                        $"Prøv venligst igen, eller kontakt support hvis problemet fortsætter.");

                    // Delete email to prevent reprocessing
                    try
                    {
                        await _emailService.DeleteEmailAsync(email.UniqueId);
                        _logger.LogInformation("Deleted failed email {UniqueId}", email.UniqueId);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "Failed to delete email {UniqueId}", email.UniqueId);
                    }
                }
            }

            _logger.LogInformation(
                "Email processing completed. Total: {Total}, Succeeded: {Success}, Failed: {Failed}",
                emailList.Count, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email processor failed: {Message}", ex.Message);
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {NextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }

    private async Task SendErrorEmailAsync(string toAddress, string originalSubject, string errorMessage)
    {
        try
        {
            var fullMessage = $"Din email med emnet '{originalSubject}' kunne ikke behandles.\n\n{errorMessage}";

            await _emailService.SendEmailAsync(
                toAddress,
                "Fejl ved behandling af din pakke-email",
                fullMessage);
            _logger.LogInformation("Sent error email to {Sender}", toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error email to {Sender}: {Message}",
                toAddress, ex.Message);
        }
    }

    private bool IsSenderExcluded(string senderEmail, string? excludedSenders)
    {
        if (string.IsNullOrWhiteSpace(excludedSenders))
            return false;

        var excludedList = excludedSenders
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e));

        return excludedList.Any(excluded =>
            excluded.Equals(senderEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractEmailAddress(string fromField)
    {
        // Extract email from formats like "Name <email@example.com>" or just "email@example.com"
        var match = System.Text.RegularExpressions.Regex.Match(fromField, @"<(.+?)>|^(.+?)$");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
