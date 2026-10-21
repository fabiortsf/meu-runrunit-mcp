using System.Text;

namespace MeuRunrunItMCP.Models;

public sealed record CommentSnippet(
    int Id,
    string Author,
    string? CreatedAt,
    string Text);

public sealed record TaskContext(
    int TaskId,
    string Title,
    string? Description,
    string? ProjectName,
    string? BoardName,
    string? Url,
    IReadOnlyList<CommentSnippet> Comments,
    string CombinedTextForSearch,
    IReadOnlyList<EmbeddedImage> Images)
{
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Tarefa #{TaskId}: {Title}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(ProjectName))
            sb.AppendLine($"**Projeto:** {ProjectName}");
        if (!string.IsNullOrWhiteSpace(BoardName))
            sb.AppendLine($"**Quadro:** {BoardName}");
        if (!string.IsNullOrWhiteSpace(Url))
            sb.AppendLine($"**URL:** {Url}");
        if (sb.Length > 0 && sb[^1] is not '\n')
            sb.AppendLine();

        sb.AppendLine("## Descrição");
        sb.AppendLine(string.IsNullOrWhiteSpace(Description) ? "_Sem descrição._" : Description.Trim());
        sb.AppendLine();
        AppendImages(sb, Images);
        sb.AppendLine();

        sb.AppendLine($"## Comentários de usuários ({Comments.Count})");
        if (Comments.Count == 0)
        {
            sb.AppendLine("_Nenhum comentário de usuário (comentários automáticos do Runrun.it foram ignorados)._");
        }
        else
        {
            foreach (var comment in Comments)
            {
                var when = string.IsNullOrWhiteSpace(comment.CreatedAt) ? "" : $" ({comment.CreatedAt})";
                sb.AppendLine($"### [{comment.Id}] {comment.Author}{when}");
                sb.AppendLine(comment.Text.Trim());
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Texto agregado para busca no código");
        sb.AppendLine("```");
        sb.AppendLine(CombinedTextForSearch);
        sb.AppendLine("```");

        return sb.ToString().TrimEnd();
    }

    internal static void AppendImages(StringBuilder sb, IReadOnlyList<EmbeddedImage> images)
    {
        sb.AppendLine($"## Imagens embutidas ({images.Count})");
        if (images.Count == 0)
        {
            sb.AppendLine("_Nenhuma imagem na descrição._");
            return;
        }

        var index = 1;
        foreach (var image in images)
        {
            sb.AppendLine($"{index}. origem: {FormatImageSource(image.Source)}");
            if (image.DocumentId is int documentId)
                sb.AppendLine($"   documento: {documentId}");
            if (!string.IsNullOrWhiteSpace(image.DownloadUrl))
                sb.AppendLine($"   download: {image.DownloadUrl}");
            index++;
        }

        sb.AppendLine();
        sb.AppendLine("A URL de download é temporária. Para o HTML completo, use `obter_html_da_descricao`.");
    }

    private static string FormatImageSource(string source)
    {
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return source;

        var comma = source.IndexOf(',');
        var header = comma > 0 ? source[..comma] : source;
        return header.Length <= 80 ? header : header[..80];
    }
}
