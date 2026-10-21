namespace MeuRunrunItMCP.Models;

public sealed record TaskAnalysisReport(
    TaskContext Task,
    CodeSearchResult CodeSearch)
{
    public string ToMarkdown()
    {
        return $"""
            {Task.ToMarkdown()}

            ---

            {CodeSearch.ToMarkdown()}

            ---

            ## Próximo passo (agente)

            Use `read_file` com o mesmo `projectRoot` nos arquivos acima para ler trechos e produzir o plano de alteração.
            Se houver imagens embutidas, abra cada URL de download antes de concluir a análise.
            """;
    }
}
