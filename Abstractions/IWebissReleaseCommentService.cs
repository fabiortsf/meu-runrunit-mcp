namespace MeuRunrunItMCP.Abstractions;

public interface IWebissReleaseCommentService
{
    Task<string> CommentNewDeliveriesAsync(CancellationToken cancellationToken = default);
}
