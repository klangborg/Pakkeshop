using Pakkeshop.Models;

namespace Pakkeshop.Services;

public interface IEmailService
{
    Task<IEnumerable<EmailMessage>> GetUnreadEmailsAsync();
    Task DeleteEmailAsync(string uniqueId);
    Task SendEmailAsync(string to, string subject, string body);
}
