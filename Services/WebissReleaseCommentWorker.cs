using MeuRunrunItMCP.Abstractions;
using MeuRunrunItMCP.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeuRunrunItMCP.Services;

public sealed class WebissReleaseCommentWorker(
    IWebissReleaseCommentService commentService,
    IOptions<WebissReleaseOptions> options,
    ILogger<WebissReleaseCommentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
            return;

        var interval = TimeSpan.FromHours(Math.Max(1, config.IntervalHours));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                var report = await commentService.CommentNewDeliveriesAsync(stoppingToken);
                logger.LogInformation("Verificação de entregas WEBISS concluída. {Report}", report);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao comentar entregas WEBISS. A próxima tentativa segue no intervalo de {IntervalHours}h.", interval.TotalHours);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
