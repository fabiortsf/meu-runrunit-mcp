namespace MeuRunrunItMCP.Models;

public sealed record ClosedTaskItem(
    int Id,
    string Title,
    string? ProjectName,
    string? ClientName,
    string? BoardStageName,
    string? EvaluationStatus,
    DateTimeOffset? CloseDate);
