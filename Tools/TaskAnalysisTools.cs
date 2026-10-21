using System.ComponentModel;
using MeuRunrunItMCP.Abstractions;
using ModelContextProtocol.Server;

namespace MeuRunrunItMCP.Tools;

[McpServerToolType]
public sealed class TaskAnalysisTools(ITaskAnalysisService taskAnalysisService)
{
    [McpServerTool(Name = "analisar_tarefa"), Description("Busca tarefa no Runrun.it (comentários e imagens embutidas na descrição) e localiza arquivos relacionados no repositório informado.")]
    public async Task<string> AnalyzeTaskAgainstCode(
        [Description("ID numérico da tarefa no Runrun.it")] int taskId,
        [Description("Caminho absoluto da raiz do repositório a analisar (ex: C:\\dev\\meu-app)")] string? projectRoot,
        [Description("Termos extras para refinar a busca no código (opcional)")] string? extraQuery,
        [Description("Número máximo de arquivos no resultado (opcional)")] int? maxResults,
        CancellationToken cancellationToken)
    {
        var report = await taskAnalysisService.AnalyzeAsync(
            taskId, projectRoot, extraQuery, maxResults, cancellationToken);
        return report.ToMarkdown();
    }
}
