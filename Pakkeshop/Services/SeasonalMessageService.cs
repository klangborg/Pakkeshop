namespace Pakkeshop.Services;

public class SeasonalMessageService : ISeasonalMessageService
{
    private readonly SeasonalCharacter _currentCharacter;

    public SeasonalMessageService()
    {
        _currentCharacter = GetSeasonalCharacter(DateTime.Now.Month);
    }

    public string GetCharacterName() => _currentCharacter.Name;
    public string GetCharacterEmoji() => _currentCharacter.Emoji;
    public string GetSystemPrompt() => _currentCharacter.SystemPrompt;
    public string GetFallbackMessage() => _currentCharacter.FallbackMessage;
    public string GetEmailSubject() => _currentCharacter.EmailSubject;

    private static SeasonalCharacter GetSeasonalCharacter(int month)
    {
        return month switch
        {
            11 or 12 => new SeasonalCharacter
            {
                Name = "julenisse",
                Emoji = "🎅",
                SystemPrompt = "Du er en hjælpsom julenisse der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og hyggeligt tone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en hyggelig nissehilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Ho ho ho! 🎅",
                EmailSubject = "Din pakke er registreret! 🎅"
            },
            1 or 2 => new SeasonalCharacter
            {
                Name = "vintermandens hjælper",
                Emoji = "❄️",
                SystemPrompt = "Du er vintermandens hjælper der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og hyggeligt tone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en vinterlig hilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Frosne hilsner! ❄️",
                EmailSubject = "Din pakke er registreret! ❄️"
            },
            3 or 4 => new SeasonalCharacter
            {
                Name = "forårshare",
                Emoji = "🐰",
                SystemPrompt = "Du er en munter forårshare der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og forårsfrisktone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en forårshilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Forårshilsner! 🐰",
                EmailSubject = "Din pakke er registreret! 🐰"
            },
            5 or 6 => new SeasonalCharacter
            {
                Name = "sommerfugl",
                Emoji = "🦋",
                SystemPrompt = "Du er en glad sommerfugl der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og solrigt tone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en sommerlig hilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Solrige hilsner! 🦋",
                EmailSubject = "Din pakke er registreret! 🦋"
            },
            7 or 8 => new SeasonalCharacter
            {
                Name = "solstråle",
                Emoji = "🌞",
                SystemPrompt = "Du er en varm solstråle der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og sommerligt tone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en sommerlig hilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Solrige sommerhilsner! 🌞",
                EmailSubject = "Din pakke er registreret! 🌞"
            },
            9 or 10 => new SeasonalCharacter
            {
                Name = "høstgoblin",
                Emoji = "🍂",
                SystemPrompt = "Du er en hjælpsom høstgoblin der arbejder med at holde styr på pakker. " +
                               "Skriv en kort, venlig og personlig besked (2-4 sætninger) der bekræfter at pakken er tilføjet til listen, " +
                               "så modtageren har overblik over de pakker der kan hentes. " +
                               "Brug et muntert og hyggeligt tone. Inkluder de vigtigste oplysninger om pakken. " +
                               "Underskrive med en høstlig hilsen.",
                FallbackMessage = "Din pakke er blevet registreret! Høstlige hilsner! 🍂",
                EmailSubject = "Din pakke er registreret! 🍂"
            },
            _ => throw new ArgumentException($"Invalid month: {month}")
        };
    }

    private class SeasonalCharacter
    {
        public required string Name { get; init; }
        public required string Emoji { get; init; }
        public required string SystemPrompt { get; init; }
        public required string FallbackMessage { get; init; }
        public required string EmailSubject { get; init; }
    }
}
