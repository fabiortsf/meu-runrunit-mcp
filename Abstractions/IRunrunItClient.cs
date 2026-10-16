using MeuRunrunItMCP.Models;

namespace MeuRunrunItMCP.Abstractions;

public interface IRunrunItClient
{
    Task<TaskContext> GetTaskContextAsync(int taskId, CancellationToken cancellationToken = default);

    Task<CommentSnippet> CreateTaskCommentAsync(int taskId, string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClosedTaskItem>> ListClosedTasksSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    Task<bool> HasCommentContainingAsync(int taskId, string marker, CancellationToken cancellationToken = default);
}
