using System.Text;
using System.Text.Json;
using MeuRunrunItMCP.Abstractions;
using MeuRunrunItMCP.Models;
using MeuRunrunItMCP.Options;
using MeuRunrunItMCP.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeuRunrunItMCP.Services;

public sealed class WebissReleaseCommentService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<WebissReleaseOptions> options,
    ILogger<WebissReleaseCommentService> logger) : IWebissReleaseCommentService
{
    private const int MaxStoredTaskIds = 5000;

    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string> CommentNewDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled)
            return "A verificação de entregas WEBISS está desligada.";

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeuRunrunItMCP");
        Directory.CreateDirectory(directory);

        var statePath = Path.Combine(directory, "webiss-release-state.json");
        var lockPath = statePath + ".lock";

        FileStream lockStream;
        try
        {
            lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return "Já existe uma verificação em andamento. Nenhum comentário novo foi publicado.";
        }

        await using (lockStream)
        {
            var state = await LoadStateAsync(statePath, cancellationToken);
            var since = ResolveSince(state, config);
            var currentVersion = await ReadCurrentVersionAsync(config, cancellationToken);
            var nextVersion = WebissVersion.Increment(currentVersion);
            var commentText = WebissVersion.FormatComment(nextVersion);

            using var scope = scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IRunrunItClient>();
            var tasks = await client.ListClosedTasksSinceAsync(since, cancellationToken);

            var commented = new List<string>();
            var skipped = new List<string>();
            var seen = new HashSet<int>();
            var knownIds = state.CommentedTaskIds.ToHashSet();
            var hitCap = false;
            var maxComments = Math.Max(1, config.MaxCommentsPerRun);

            foreach (var task in tasks)
            {
                if (!seen.Add(task.Id) || !Qualifies(task, config.ClientName))
                    continue;

                if (knownIds.Contains(task.Id))
                {
                    skipped.Add($"#{task.Id} já comentada");
                    continue;
                }

                if (commented.Count >= maxComments)
                {
                    hitCap = true;
                    break;
                }

                if (await client.HasCommentContainingAsync(task.Id, WebissReleaseNotesMarker(), cancellationToken))
                {
                    knownIds.Add(task.Id);
                    skipped.Add($"#{task.Id} já tem o comentário de versão");
                    continue;
                }

                var created = await client.CreateTaskCommentAsync(task.Id, commentText, cancellationToken);
                knownIds.Add(task.Id);
                commented.Add($"#{task.Id} {task.Title} (comentário {created.Id})");
                logger.LogInformation("Comentário de versão publicado na tarefa {TaskId}.", task.Id);
            }

            if (!hitCap)
                state.LastRunUtc = DateTimeOffset.UtcNow;

            state.CommentedTaskIds = knownIds.TakeLast(MaxStoredTaskIds).ToList();
            await SaveStateAsync(statePath, state, cancellationToken);

            return BuildReport(currentVersion, nextVersion, since, commented, skipped, hitCap, maxComments);
        }
    }

    private static string WebissReleaseNotesMarker() => WebissVersion.CommentPrefix;

    private static bool Qualifies(ClosedTaskItem task, string clientName)
    {
        var isWebiss = string.Equals(task.ClientName, clientName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(task.ProjectName, clientName, StringComparison.OrdinalIgnoreCase);
        if (!isWebiss)
            return false;

        if (task.BoardStageName?.Contains("cancel", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        return string.Equals(task.EvaluationStatus, "approved", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ResolveSince(ReleaseState state, WebissReleaseOptions config)
    {
        if (state.LastRunUtc is DateTimeOffset lastRun)
            return lastRun.Subtract(TimeSpan.FromMinutes(Math.Max(0, config.OverlapMinutes)));

        var lookbackHours = Math.Max(1, config.InitialLookbackHours);
        return DateTimeOffset.UtcNow.AddHours(-lookbackHours);
    }

    private async Task<string> ReadCurrentVersionAsync(WebissReleaseOptions config, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("webiss-version");
        using var response = await client.GetAsync(config.VersionPageUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return WebissVersion.ReadFromHtml(html);
    }

    private static async Task<ReleaseState> LoadStateAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new ReleaseState();

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ReleaseState>(stream, StateJsonOptions, cancellationToken)
                ?? new ReleaseState();
        }
        catch (JsonException)
        {
            return new ReleaseState();
        }
    }

    private static async Task SaveStateAsync(string path, ReleaseState state, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, StateJsonOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static string BuildReport(
        string currentVersion,
        string nextVersion,
        DateTimeOffset since,
        IReadOnlyList<string> commented,
        IReadOnlyList<string> skipped,
        bool hitCap,
        int maxComments)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Versão publicada: {currentVersion}");
        sb.AppendLine($"Comentário: {WebissVersion.FormatComment(nextVersion)}");
        sb.AppendLine($"Janela desde: {since:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine($"Comentários novos: {commented.Count}");
        if (commented.Count == 0)
            sb.AppendLine("_Nenhuma tarefa nova para comentar._");
        else
            foreach (var line in commented)
                sb.AppendLine($"- {line}");

        sb.AppendLine();
        sb.AppendLine($"Ignoradas (já comentadas): {skipped.Count}");
        foreach (var line in skipped.Take(30))
            sb.AppendLine($"- {line}");

        if (hitCap)
        {
            sb.AppendLine();
            sb.AppendLine($"Limite de {maxComments} comentários nesta execução. A próxima verificação continua de onde parou.");
        }

        return sb.ToString().TrimEnd();
    }

    private sealed class ReleaseState
    {
        public DateTimeOffset? LastRunUtc { get; set; }

        public List<int> CommentedTaskIds { get; set; } = [];
    }
}
