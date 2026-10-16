namespace MeuRunrunItMCP.Abstractions;

public interface IWebissReleaseCommentService
{
    Task<string> CommentNewDeliveriesAsync(string? siteUrl = null, CancellationToken cancellationToken = default);
}
