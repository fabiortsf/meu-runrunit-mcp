using System.Text.RegularExpressions;

namespace MeuRunrunItMCP.Utilities;

internal static partial class WebissVersion
{
    public const string CommentPrefix = "Disponível a partir da versão ";

    [GeneratedRegex(@"Vers[aã]o:\s*(\d+\.\d+\.\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageVersionRegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)\.(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    public static string FormatComment(string version) =>
        $"{CommentPrefix}{version}.\n<i>Automated comment by Cursor IA</i>";

    public static bool ContainsMarker(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains(CommentPrefix, StringComparison.OrdinalIgnoreCase);

    public static string ReadFromHtml(string html, string pageUrl)
    {
        var match = PageVersionRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Não encontrei a versão no rodapé de {pageUrl}. O texto esperado é 'Versão: 5.3.0.43'.");
        }

        return match.Groups[1].Value;
    }

    public static string Increment(string version)
    {
        var match = VersionRegex().Match(version.Trim());
        if (!match.Success || !int.TryParse(match.Groups[4].Value, out var revision))
        {
            throw new InvalidOperationException($"Versão '{version}' não está no formato 5.3.0.43.");
        }

        return $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}.{revision + 1}";
    }
}
