using System.Text;

namespace MeuRunrunItMCP.Models;

public sealed record EmbeddedImage(
    string Source,
    int? DocumentId,
    string? DownloadUrl);

public sealed record TaskDescriptionHtml(
    int TaskId,
    string? Html,
    IReadOnlyList<EmbeddedImage> Images)
{
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# HTML da descrição — tarefa #{TaskId}");
        sb.AppendLine();
        sb.AppendLine("## HTML");
        if (string.IsNullOrWhiteSpace(Html))
        {
            sb.AppendLine("_Sem descrição._");
        }
        else
        {
            sb.AppendLine("```html");
            sb.AppendLine(Html.Trim());
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine($"## Imagens embutidas ({Images.Count})");
        if (Images.Count == 0)
        {
            sb.AppendLine("_Nenhuma imagem na descrição._");
            return sb.ToString().TrimEnd();
        }

        var index = 1;
        foreach (var image in Images)
        {
            sb.AppendLine($"{index}. origem: {FormatSource(image.Source)}");
            if (image.DocumentId is int documentId)
                sb.AppendLine($"   documento: {documentId}");
            if (!string.IsNullOrWhiteSpace(image.DownloadUrl))
                sb.AppendLine($"   download: {image.DownloadUrl}");
            else if (image.Source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine("   download: o conteúdo está em base64 no HTML");
            index++;
        }

        sb.AppendLine();
        sb.AppendLine("A URL de download é temporária.");
        return sb.ToString().TrimEnd();
    }

    private static string FormatSource(string source)
    {
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return source;

        var comma = source.IndexOf(',');
        var header = comma > 0 ? source[..comma] : source;
        return header.Length <= 80 ? header : header[..80];
    }
}
