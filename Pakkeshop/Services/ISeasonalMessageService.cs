namespace Pakkeshop.Services;

public interface ISeasonalMessageService
{
    string GetCharacterName();
    string GetCharacterEmoji();
    string GetSystemPrompt();
    string GetFallbackMessage();
    string GetEmailSubject();
}
