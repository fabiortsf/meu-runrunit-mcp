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

    [McpServerTool(Name = "obter_html_da_descricao"), Description("Retorna o HTML completo da descrição da tarefa e as imagens embutidas. Cada imagem de documento inclui a URL temporária de download.")]
    public async Task<string> GetTaskDescriptionHtml(
        [Description("ID numérico da tarefa no Runrun.it")] int taskId,
        CancellationToken cancellationToken)
    {
        var description = await runrunItClient.GetTaskDescriptionHtmlAsync(taskId, cancellationToken);
        return description.ToMarkdown();
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

    [McpServerTool(Name = "comentar_entregas_webiss"), Description("Lista tarefas encerradas e homologadas do WebISS atribuídas ao usuário do token (pelo id ou pelo nome) e comenta, uma única vez, a próxima versão do ambiente informado. A URL não vem pronta: use a página inicial daquele município. A versão corrente está no rodapé, no texto 'Versão: 5.3.0.43'. O comentário soma 1 no último número.")]
    public Task<string> CommentWebissDeliveries(
        [Description("URL da página inicial do ambiente WebISS, por exemplo o portal do município. Leia o rodapé no formato Versão: 5.3.0.43.")] string siteUrl,
        CancellationToken cancellationToken) =>
        releaseComments.CommentNewDeliveriesAsync(siteUrl, cancellationToken);
}
