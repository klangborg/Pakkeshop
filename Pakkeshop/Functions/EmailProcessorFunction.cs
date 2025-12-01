using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pakkeshop.Services;

namespace Pakkeshop.Functions;

public class EmailProcessorFunction
{
    private readonly ILogger<EmailProcessorFunction> _logger;
    private readonly IEmailService _emailService;
    private readonly IOpenAIService _openAIService;
    private readonly IGoogleSheetsService _sheetsService;

    public EmailProcessorFunction(
        ILogger<EmailProcessorFunction> logger,
        IEmailService emailService,
        IOpenAIService openAIService,
        IGoogleSheetsService sheetsService)
    {
        _logger = logger;
        _emailService = emailService;
        _openAIService = openAIService;
        _sheetsService = sheetsService;
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

                    if (packageData != null)
                    {
                        await _sheetsService.AppendRowAsync(packageData);

                        // Send success email with elf message
                        try
                        {
                            var elfMessage = await _openAIService.GenerateElfResponseAsync(packageData);
                            await _emailService.SendEmailAsync(
                                senderEmail,
                                "Din pakke er registreret! 🎅",
                                elfMessage);
                            _logger.LogInformation("Sent success email to {Sender}", senderEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "Failed to send success email to {Sender}", senderEmail);
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

    private static string ExtractEmailAddress(string fromField)
    {
        // Extract email from formats like "Name <email@example.com>" or just "email@example.com"
        var match = System.Text.RegularExpressions.Regex.Match(fromField, @"<(.+?)>|^(.+?)$");
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }
}
