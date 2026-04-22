using System.Text.RegularExpressions;

namespace Pakkeshop.Services;

public static partial class PostNordLinkMatcher
{
    [GeneratedRegex(@"https://l\.postnord\.com/[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex ShortLinkPattern();

    public static string? FirstMatch(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var m = ShortLinkPattern().Match(text);
        return m.Success ? m.Value : null;
    }
}
