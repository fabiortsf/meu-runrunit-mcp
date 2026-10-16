using System.Reflection;
using MeuRunrunItMCP.Abstractions;
using MeuRunrunItMCP.Options;
using MeuRunrunItMCP.Services;
using MeuRunrunItMCP.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeuRunrunItMCP;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

        builder.Services
            .Configure<RunrunItOptions>(builder.Configuration.GetSection(RunrunItOptions.SectionName))
            .Configure<CodeAnalysisOptions>(builder.Configuration.GetSection(CodeAnalysisOptions.SectionName))
            .Configure<WebissReleaseOptions>(builder.Configuration.GetSection(WebissReleaseOptions.SectionName));

        // Default host logging on Windows includes EventLog, which fails when published
        // or run with roll-forward on a newer runtime (missing System.Diagnostics.EventLog).
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(consoleLogOptions => consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddHttpClient<IRunrunItClient, RunrunItClient>();
        builder.Services.AddHttpClient("webiss-version", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MeuRunrunItMCP/1.0");
        });
        builder.Services.AddSingleton<ICodeIndexService, CodeIndexService>();
        builder.Services.AddSingleton<ITaskAnalysisService, TaskAnalysisService>();
        builder.Services.AddSingleton<IWebissReleaseCommentService, WebissReleaseCommentService>();
        builder.Services.AddHostedService<WebissReleaseCommentWorker>();

        builder.Services.AddSingleton<RunrunItTools>();
        builder.Services.AddSingleton<CodeAnalysisTools>();
        builder.Services.AddSingleton<TaskAnalysisTools>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }
}
