using System.Text.RegularExpressions;

namespace MeuRunrunItMCP.Utilities;

internal static partial class PullRequestLink
{
    [GeneratedRegex(
        @"https?://(?:www\.)?github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/pull/\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    public static string? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = UrlRegex().Match(text);
        return match.Success ? match.Value : null;
    }
}
