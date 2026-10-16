using System.ComponentModel;
using MeuRunrunItMCP.Abstractions;
using ModelContextProtocol.Server;

namespace MeuRunrunItMCP.Tools;

[McpServerToolType]
public sealed class RunrunItTools(IRunrunItClient runrunItClient, IWebissReleaseCommentService releaseComments)
{
    [McpServerTool(Name = "obter_informacoes_da_tarefa"), Description("Retorna o contexto completo de uma tarefa Runrun.it (título, descrição e todos os comentários).")]
    public async Task<string> GetTaskContext(
        [Description("ID numérico da tarefa no Runrun.it")] int taskId,
        CancellationToken cancellationToken)
    {
        var context = await runrunItClient.GetTaskContextAsync(taskId, cancellationToken);
        return context.ToMarkdown();
    }

    [McpServerTool(Name = "comentar_tarefa"), Description("Publica um comentário de usuário em uma tarefa do Runrun.it.")]
    public async Task<string> CreateTaskComment(
        [Description("ID numérico da tarefa no Runrun.it")] int taskId,
        [Description("Texto do comentário a publicar na tarefa")] string text,
        CancellationToken cancellationToken)
    {
        var comment = await runrunItClient.CreateTaskCommentAsync(taskId, text, cancellationToken);
        var when = string.IsNullOrWhiteSpace(comment.CreatedAt) ? "" : $" ({comment.CreatedAt})";

        return $"""
            Comentário publicado na tarefa #{taskId}.

            ### [{comment.Id}] {comment.Author}{when}
            {comment.Text.Trim()}
            """;
    }

    [McpServerTool(Name = "comentar_entregas_webiss"), Description("Lista tarefas encerradas e homologadas do WebISS e comenta, uma única vez, a próxima versão de https://aracajuse.webiss.com.br.")]
    public Task<string> CommentWebissDeliveries(CancellationToken cancellationToken) =>
        releaseComments.CommentNewDeliveriesAsync(cancellationToken);
}
