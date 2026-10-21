using System.Net;
using System.Text.RegularExpressions;

namespace MeuRunrunItMCP.Utilities;

internal readonly record struct EmbeddedImageRef(string Source, int? DocumentId);

internal static partial class DescriptionImages
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex("""(?<=\s)src\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SrcRegex();

    [GeneratedRegex("""\bdata-document-id\s*=\s*(?:"(\d+)"|'(\d+)'|(\d+))""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DataDocumentIdRegex();

    [GeneratedRegex(@"/documents/(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentPathRegex();

    public static IReadOnlyList<EmbeddedImageRef> Extract(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var result = new List<EmbeddedImageRef>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match tag in ImgTagRegex().Matches(html))
        {
            var documentId = ReadDocumentId(tag.Value);
            var source = ReadSource(tag.Value);
            if (string.IsNullOrWhiteSpace(source) && documentId is int idFromTag)
                source = $"documents/{idFromTag}/download";
            if (string.IsNullOrWhiteSpace(source) || !seen.Add(source))
                continue;

            result.Add(new EmbeddedImageRef(source, documentId));
        }

        return result;
    }

    private static string? ReadSource(string tag)
    {
        var match = SrcRegex().Match(tag);
        if (!match.Success)
            return null;

        var raw = match.Groups[1].Success ? match.Groups[1].Value
            : match.Groups[2].Success ? match.Groups[2].Value
            : match.Groups[3].Value;

        var decoded = WebUtility.HtmlDecode(raw).Trim();
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    private static int? ReadDocumentId(string tag)
    {
        var explicitId = DataDocumentIdRegex().Match(tag);
        if (explicitId.Success && TryReadId(explicitId, out var id))
            return id;

        var fromPath = DocumentPathRegex().Match(tag);
        if (fromPath.Success && int.TryParse(fromPath.Groups[1].Value, out id))
            return id;

        return null;
    }

    private static bool TryReadId(Match match, out int id)
    {
        for (var group = 1; group < match.Groups.Count; group++)
        {
            if (match.Groups[group].Success && int.TryParse(match.Groups[group].Value, out id))
                return true;
        }

        id = 0;
        return false;
    }
}
