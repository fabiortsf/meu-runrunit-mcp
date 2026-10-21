using MeuRunrunItMCP.Models;

namespace MeuRunrunItMCP.Abstractions;

public interface IRunrunItClient
{
    Task<TaskContext> GetTaskContextAsync(int taskId, CancellationToken cancellationToken = default);

    Task<TaskDescriptionHtml> GetTaskDescriptionHtmlAsync(int taskId, CancellationToken cancellationToken = default);

    Task<CommentSnippet> CreateTaskCommentAsync(int taskId, string text, CancellationToken cancellationToken = default);

    Task<RunrunUser> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClosedTaskItem>> ListClosedTasksSinceAsync(
        DateTimeOffset since,
        RunrunUser assignee,
        CancellationToken cancellationToken = default);

    Task<bool> HasCommentContainingAsync(int taskId, string marker, CancellationToken cancellationToken = default);

    Task<TaskReleaseSignals> InspectReleaseCommentsAsync(
        int taskId,
        string versionMarker,
        CancellationToken cancellationToken = default);
}
