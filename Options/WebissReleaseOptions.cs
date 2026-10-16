namespace MeuRunrunItMCP.Options;

public sealed class WebissReleaseOptions
{
    public const string SectionName = "WebissRelease";

    public bool Enabled { get; set; } = true;

    public string ClientName { get; set; } = "WebISS";

    public string VersionPageUrl { get; set; } = "https://aracajuse.webiss.com.br";

    public int IntervalHours { get; set; } = 1;

    public int InitialLookbackHours { get; set; } = 1;

    public int OverlapMinutes { get; set; } = 15;

    public int MaxCommentsPerRun { get; set; } = 20;
}
