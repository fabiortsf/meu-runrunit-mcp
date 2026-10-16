using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MeuRunrunItMCP.Abstractions;
using MeuRunrunItMCP.Models;
using MeuRunrunItMCP.Options;
using MeuRunrunItMCP.Utilities;
using Microsoft.Extensions.Options;

namespace MeuRunrunItMCP.Services;

public sealed class RunrunItClient(HttpClient httpClient, IOptions<RunrunItOptions> options) : IRunrunItClient
{
    public async Task<TaskContext> GetTaskContextAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        ValidateCredentials(config);

        using var taskRequest = CreateRequest(HttpMethod.Get, config, $"tasks/{taskId}");
        using var taskResponse = await httpClient.SendAsync(taskRequest, cancellationToken);
        await EnsureSuccessAsync(taskResponse, cancellationToken);

        await using var taskStream = await taskResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var taskDoc = await JsonDocument.ParseAsync(taskStream, cancellationToken: cancellationToken);
        var taskRoot = UnwrapRoot(taskDoc.RootElement);

        var title = GetString(taskRoot, "title") ?? $"Tarefa {taskId}";
        var descriptionRaw = await GetTaskDescriptionAsync(config, taskId, taskRoot, cancellationToken);
        var projectName = GetString(taskRoot, "project_name");
        var boardName = GetString(taskRoot, "board_name");
        var url = GetString(taskRoot, "url");

        var comments = await GetCommentsAsync(config, taskId, cancellationToken);
        var combined = TextHelper.BuildCombinedSearchText(title, descriptionRaw, comments);

        return new TaskContext(
            taskId,
            title,
            string.IsNullOrWhiteSpace(descriptionRaw) ? null : TextHelper.StripHtml(descriptionRaw),
            projectName,
            boardName,
            url,
            comments,
            combined);
    }

    public async Task<CommentSnippet> CreateTaskCommentAsync(
        int taskId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        ValidateCredentials(config);

        if (taskId <= 0)
            throw new ArgumentOutOfRangeException(nameof(taskId), "O ID da tarefa deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("O texto do comentário não pode ser vazio.", nameof(text));

        var payload = JsonSerializer.Serialize(new
        {
            task_id = taskId,
            text = text.Trim()
        });

        using var request = CreateRequest(HttpMethod.Post, config, "comments");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = UnwrapComment(doc.RootElement);

        var id = GetInt(root, "id") ?? 0;
        var commentText = TextHelper.StripHtml(GetString(root, "text"));
        if (string.IsNullOrWhiteSpace(commentText))
            commentText = text.Trim();
        var author = GetCommentAuthor(root);
        var createdAt = GetString(root, "created_at");

        return new CommentSnippet(id, author, createdAt, commentText);
    }

    public async Task<IReadOnlyList<ClosedTaskItem>> ListClosedTasksSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        ValidateCredentials(config);

        var result = new List<ClosedTaskItem>();
        const int pageSize = 100;

        for (var page = 1; page <= 20; page++)
        {
            var query = new Dictionary<string, string>
            {
                ["is_closed"] = "true",
                ["sort"] = "close_date",
                ["sort_dir"] = "desc",
                ["limit"] = pageSize.ToString(),
                ["page"] = page.ToString()
            };

            using var request = CreateRequest(HttpMethod.Get, config, "tasks", query);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var batch = ParseClosedTasks(doc.RootElement);
            if (batch.Count == 0)
                break;

            var reachedOlder = false;
            foreach (var task in batch)
            {
                if (task.CloseDate is null)
                    continue;

                if (task.CloseDate < since)
                {
                    reachedOlder = true;
                    break;
                }

                result.Add(task);
            }

            if (reachedOlder || batch.Count < pageSize)
                break;
        }

        return result;
    }

    public async Task<bool> HasCommentContainingAsync(
        int taskId,
        string marker,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        ValidateCredentials(config);

        using var request = CreateRequest(HttpMethod.Get, config, $"tasks/{taskId}/comments");
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        foreach (var element in EnumerateCommentElements(doc.RootElement))
        {
            var commentElement = element.TryGetProperty("comment", out var wrapped) ? wrapped : element;
            var text = TextHelper.StripHtml(GetString(commentElement, "text") ?? GetString(commentElement, "body"));
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<string?> GetTaskDescriptionAsync(
        RunrunItOptions config,
        int taskId,
        JsonElement taskRoot,
        CancellationToken cancellationToken)
    {
        var fromTaskDescriptionEndpoint = await GetDescriptionFromTaskDescriptionApiAsync(config, taskId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(fromTaskDescriptionEndpoint))
            return fromTaskDescriptionEndpoint;

        return ExtractDescriptionFromElement(taskRoot);
    }

    private async Task<string?> GetDescriptionFromTaskDescriptionApiAsync(
        RunrunItOptions config,
        int taskId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, config, $"tasks/{taskId}/description");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseDescriptionPayload(doc.RootElement);
    }

    private static string? ParseDescriptionPayload(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var text = ExtractDescriptionFromElement(item);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("description", out var descriptionNode))
        {
            var direct = ExtractDescriptionFromProperty(descriptionNode);
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
        }

        if (root.TryGetProperty("data", out var data))
            return ParseDescriptionPayload(data);

        if (root.TryGetProperty("descriptions", out var descriptions))
            return ParseDescriptionPayload(descriptions);

        return ExtractDescriptionFromElement(root);
    }

    private static string? ExtractDescriptionFromElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty("description", out var descriptionNode))
        {
            var nested = ExtractDescriptionFromProperty(descriptionNode);
            if (!string.IsNullOrWhiteSpace(nested))
                return nested;
        }

        return GetString(element, "body")
            ?? GetString(element, "html")
            ?? GetString(element, "text");
    }

    private static string? ExtractDescriptionFromProperty(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => GetString(value, "description")
                ?? GetString(value, "body")
                ?? GetString(value, "html")
                ?? GetString(value, "text"),
            _ => null
        };

    private async Task<IReadOnlyList<CommentSnippet>> GetCommentsAsync(
        RunrunItOptions config,
        int taskId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, config, $"tasks/{taskId}/comments");
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseComments(doc.RootElement);
    }

    private static List<ClosedTaskItem> ParseClosedTasks(JsonElement root)
    {
        var result = new List<ClosedTaskItem>();
        foreach (var element in EnumerateTaskElements(root))
        {
            var task = element.TryGetProperty("task", out var wrapped) ? wrapped : element;
            var id = GetInt(task, "id");
            if (id is null or <= 0)
                continue;

            result.Add(new ClosedTaskItem(
                id.Value,
                GetString(task, "title") ?? $"Tarefa {id}",
                GetString(task, "project_name"),
                GetString(task, "client_name"),
                GetString(task, "board_stage_name"),
                GetString(task, "evaluation_status"),
                ParseDate(task, "close_date")));
        }

        return result;
    }

    private static IEnumerable<JsonElement> EnumerateTaskElements(JsonElement root) =>
        root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array
                => tasks.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                => data.EnumerateArray(),
            _ => []
        };

    private static IEnumerable<JsonElement> EnumerateCommentElements(JsonElement root) =>
        root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("comments", out var comments) && comments.ValueKind == JsonValueKind.Array
                => comments.EnumerateArray(),
            JsonValueKind.Object when root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                => data.EnumerateArray(),
            _ => []
        };

    private static DateTimeOffset? ParseDate(JsonElement element, string propertyName)
    {
        var raw = GetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static IReadOnlyList<CommentSnippet> ParseComments(JsonElement root)
    {
        var elements = EnumerateCommentElements(root);

        var result = new List<CommentSnippet>();
        foreach (var element in elements)
        {
            var commentElement = element.TryGetProperty("comment", out var wrapped) ? wrapped : element;

            if (!CommentFilter.IsUserComment(commentElement))
                continue;

            var id = GetInt(commentElement, "id") ?? 0;
            var text = TextHelper.StripHtml(GetString(commentElement, "text") ?? GetString(commentElement, "body"));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var author = GetCommentAuthor(commentElement);
            var createdAt = GetString(commentElement, "created_at")
                ?? GetString(commentElement, "happened_at");

            result.Add(new CommentSnippet(id, author, createdAt, text));
        }

        return result;
    }

    private static string GetCommentAuthor(JsonElement element)
    {
        if (element.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            return GetString(user, "name")
                ?? GetString(user, "id")
                ?? GetString(user, "email")
                ?? "Desconhecido";
        }

        return GetString(element, "commenter_name")
            ?? GetString(element, "user_name")
            ?? GetString(element, "author_name")
            ?? GetString(element, "performer_name")
            ?? "Desconhecido";
    }

    private static JsonElement UnwrapComment(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("comment", out var comment))
            return comment;

        return root;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        RunrunItOptions config,
        string relativePath,
        IReadOnlyDictionary<string, string>? query = null)
    {
        var baseUrl = config.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{relativePath}";

        if (query is { Count: > 0 })
        {
            var queryString = string.Join(
                "&",
                query.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
            url = $"{url}?{queryString}";
        }

        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("App-Key", config.AppKey);
        request.Headers.Add("User-Token", config.UserToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static void ValidateCredentials(RunrunItOptions config)
    {
        if (string.IsNullOrWhiteSpace(config.AppKey) || string.IsNullOrWhiteSpace(config.UserToken))
        {
            throw new InvalidOperationException(
                "Credenciais Runrun.it ausentes. Configure RunrunIt:AppKey e RunrunIt:UserToken em User Secrets ou variáveis de ambiente.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Runrun.it retornou {(int)response.StatusCode} ({response.ReasonPhrase}). Corpo: {Truncate(body, 500)}");
    }

    private static JsonElement UnwrapRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("task", out var task))
            return task;

        return root;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            return number;

        return null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
