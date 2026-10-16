namespace MeuRunrunItMCP.Options;

public sealed class WebissReleaseOptions
{
    public const string SectionName = "WebissRelease";

    public bool Enabled { get; set; } = true;

    public string ClientName { get; set; } = "WebISS";

    /// <summary>
    /// Página inicial do ambiente, opcional. Sem valor, a verificação automática não comenta
    /// e a tool exige a URL na chamada.
    /// </summary>
    public string VersionPageUrl { get; set; } = "";

    public int IntervalHours { get; set; } = 1;

    /// <summary>Quantas horas para trás a listagem de tarefas encerradas considera em toda verificação.</summary>
    public int LookbackHours { get; set; } = 24;

    public int OverlapMinutes { get; set; } = 15;

    public int MaxCommentsPerRun { get; set; } = 20;
}
