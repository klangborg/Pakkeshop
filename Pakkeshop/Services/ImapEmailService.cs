using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Pakkeshop.Configuration;
using Pakkeshop.Models;

namespace Pakkeshop.Services;

public class ImapEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<ImapEmailService> _logger;

    public ImapEmailService(IOptions<EmailSettings> settings, ILogger<ImapEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<EmailMessage>> GetUnreadEmailsAsync()
    {
        var emails = new List<EmailMessage>();

        try
        {
            using var client = new ImapClient();

            await client.ConnectAsync(_settings.ImapServer, _settings.ImapPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            var unreadUids = await inbox.SearchAsync(SearchQuery.NotSeen);

            _logger.LogInformation("Found {Count} unread emails", unreadUids.Count);

            foreach (var uid in unreadUids)
            {
                var message = await inbox.GetMessageAsync(uid);

                emails.Add(new EmailMessage
                {
                    UniqueId = uid.ToString(),
                    Subject = message.Subject ?? string.Empty,
                    From = message.From.ToString(),
                    Body = message.TextBody ?? message.HtmlBody ?? string.Empty,
                    ReceivedDate = message.Date.DateTime
                });
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unread emails: {Message}", ex.Message);
            return Enumerable.Empty<EmailMessage>();
        }

        return emails;
    }

    public async Task DeleteEmailAsync(string uniqueId)
    {
        try
        {
            using var client = new ImapClient();

            await client.ConnectAsync(_settings.ImapServer, _settings.ImapPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            var uid = new UniqueId(uint.Parse(uniqueId));
            await inbox.AddFlagsAsync(uid, MessageFlags.Deleted, true);
            await inbox.ExpungeAsync();

            _logger.LogInformation("Deleted email with ID: {UniqueId}", uniqueId);

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete email {UniqueId}: {Message}", uniqueId, ex.Message);
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                TextBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Message}", to, ex.Message);
            throw;
        }
    }
}
