using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class IgdbRefreshWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<IgdbRefreshOptions> optionsMonitor,
    ILogger<IgdbRefreshWorker> logger) : BackgroundService
{
    private const string WorkerName = "IgdbRefresh";
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IOptionsMonitor<IgdbRefreshOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<IgdbRefreshWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
            var delay = _optionsMonitor.CurrentValue.PollingInterval;
            if (delay < TimeSpan.FromSeconds(5))
            {
                delay = TimeSpan.FromSeconds(5);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    internal async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Start new IGDB refresh cycle");

        var options = _optionsMonitor.CurrentValue;
        using var scope = _scopeFactory.CreateScope();
        var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
        var workerState = await workerRepository.GetWorkerStateAsync(WorkerName, stoppingToken);
        if (workerState is null)
        {
            workerState = new WorkerStateDto(WorkerName, null, DateTime.UtcNow, true);
            await workerRepository.UpdateWorkerStateAsync(workerState, stoppingToken);
        }

        if (!workerState.IsEnabled)
        {
            _logger.LogInformation("Worker {WorkerName} disabled via kill switch.", WorkerName);
            return;
        }

        var retryBefore = DateTime.UtcNow.Subtract(options.RetryInterval);
        var missingIgdbQueue = await workerRepository.GetMissingIgdbQueueAsync(retryBefore, stoppingToken);
        if (missingIgdbQueue.Count == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentRequests));
        var tasks = missingIgdbQueue.Select(item => ProcessQueueItemAsync(item, semaphore, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessQueueItemAsync(
        GameEnrichmentQueueItem item,
        SemaphoreSlim semaphore,
        CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var twitchService = scope.ServiceProvider.GetRequiredService<ITwitchService>();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            var streamerStatsService = scope.ServiceProvider.GetRequiredService<IStreamerStatsService>();

            var twitchInfo = await twitchService.GetGameInfos(item.TwitchId);
            if (!string.IsNullOrWhiteSpace(twitchInfo.IgdbId))
            {
                await workerRepository.CompleteGameIgdbRefreshAsync(item.GameId, twitchInfo.IgdbId, stoppingToken);
                var streamerIds = await workerRepository.GetStreamerIdsByGameIdAsync(item.GameId, stoppingToken);
                await streamerStatsService.UpdateGamingRatioAsync(streamerIds, stoppingToken);
            }
            else
            {
                await workerRepository.MarkGameMissingIgdbAsync(item.GameId, DateTime.UtcNow, stoppingToken);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to refresh IGDB id for game {GameId}.", item.GameId);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
