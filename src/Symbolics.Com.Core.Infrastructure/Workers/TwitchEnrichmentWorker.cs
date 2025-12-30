using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchEnrichmentWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TwitchEnrichmentOptions> optionsMonitor,
    ILogger<TwitchEnrichmentWorker> logger) : BackgroundService
{
    private const string WorkerName = "Enrichment";
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IOptionsMonitor<TwitchEnrichmentOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<TwitchEnrichmentWorker> _logger = logger;

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
        _logger.LogInformation("Start new Twitch enrichment cycle");

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

        var streamerQueue = await workerRepository.GetStreamerEnrichmentQueueAsync(options.StreamerMaxRetryCount, stoppingToken);
        var gameQueue = await workerRepository.GetGameEnrichmentQueueAsync(options.GameMaxRetryCount, stoppingToken);

        if (streamerQueue.Count == 0 && gameQueue.Count == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentRequests));

        var streamerTasks = streamerQueue.Select(item => ProcessStreamerAsync(item, semaphore, stoppingToken));
        var gameTasks = gameQueue.Select(item => ProcessGameAsync(item, semaphore, stoppingToken));

        await Task.WhenAll(streamerTasks.Concat(gameTasks));
    }

    private async Task ProcessStreamerAsync(
        StreamerEnrichmentQueueItem item,
        SemaphoreSlim semaphore,
        CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var twitchService = scope.ServiceProvider.GetRequiredService<ITwitchService>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var consumptionTracker = scope.ServiceProvider.GetRequiredService<IConsumptionTracker>();
            var qdrantClient = scope.ServiceProvider.GetRequiredService<IQdrantClient>();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            var streamerStatsService = scope.ServiceProvider.GetRequiredService<IStreamerStatsService>();

            var twitchInfo = await twitchService.GetStreamerInfos(item.TwitchLogin);
            var streamerUrl = $"https://www.twitch.tv/{twitchInfo.Login}";
            var aiDescriptions = await aiService.GenerateStreamerDescription(
                twitchInfo.Id,
                twitchInfo.Login,
                twitchInfo.DisplayName,
                streamerUrl,
                twitchInfo.Description);
            await consumptionTracker.LogAsync(
                "Gemini",
                "GenerateStreamerDescription",
                aiDescriptions.Consumption.Model,
                aiDescriptions.Consumption.InputUnits,
                aiDescriptions.Consumption.OutputUnits,
                aiDescriptions.Consumption.CachedUnits,
                aiDescriptions.Consumption.ProcessingTimeMs);

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            await consumptionTracker.LogAsync(
                "Gemini",
                "GenerateEmbedding",
                embedding.Consumption.Model,
                embedding.Consumption.InputUnits,
                embedding.Consumption.OutputUnits,
                embedding.Consumption.CachedUnits,
                embedding.Consumption.ProcessingTimeMs);
            if (!qdrantClient.SaveStreamerDescription(item.StreamerId, embedding.Vector, aiDescriptions.VectorDescription, string.Empty))
            {
                throw new InvalidOperationException($"Failed to save streamer embedding for {item.StreamerId}.");
            }

            var update = new StreamerEnrichmentUpdate(
                item.StreamerId,
                twitchInfo.Id,
                twitchInfo.Login,
                twitchInfo.DisplayName,
                aiDescriptions.VectorDescription,
                aiDescriptions.PersonaDescription,
                aiDescriptions.Email,
                DateTime.UtcNow);

            await workerRepository.FinalizeStreamerEnrichmentAsync(update, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to enrich streamer {StreamerId}.", item.StreamerId);
            using var scope = _scopeFactory.CreateScope();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            await workerRepository.IncrementStreamerRetryAsync(item.StreamerId, stoppingToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task ProcessGameAsync(
        GameEnrichmentQueueItem item,
        SemaphoreSlim semaphore,
        CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var twitchService = scope.ServiceProvider.GetRequiredService<ITwitchService>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var consumptionTracker = scope.ServiceProvider.GetRequiredService<IConsumptionTracker>();
            var qdrantClient = scope.ServiceProvider.GetRequiredService<IQdrantClient>();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            var streamerStatsService = scope.ServiceProvider.GetRequiredService<IStreamerStatsService>();

            var twitchInfo = await twitchService.GetGameInfos(item.TwitchId);
            var aiDescriptions = await aiService.GenerateGameDescription(twitchInfo.Id, twitchInfo.Name);
            await consumptionTracker.LogAsync(
                "Gemini",
                "GenerateGameDescription",
                aiDescriptions.Consumption.Model,
                aiDescriptions.Consumption.InputUnits,
                aiDescriptions.Consumption.OutputUnits,
                aiDescriptions.Consumption.CachedUnits,
                aiDescriptions.Consumption.ProcessingTimeMs);

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            await consumptionTracker.LogAsync(
                "Gemini",
                "GenerateEmbedding",
                embedding.Consumption.Model,
                embedding.Consumption.InputUnits,
                embedding.Consumption.OutputUnits,
                embedding.Consumption.CachedUnits,
                embedding.Consumption.ProcessingTimeMs);
            if (!qdrantClient.SaveGameDescription(item.GameId, embedding.Vector, aiDescriptions.VectorDescription))
            {
                throw new InvalidOperationException($"Failed to save game embedding for {item.GameId}.");
            }

            var update = new GameEnrichmentUpdate(
                item.GameId,
                twitchInfo.Id,
                twitchInfo.Name,
                aiDescriptions.VectorDescription,
                string.IsNullOrWhiteSpace(twitchInfo.IgdbId) ? null : twitchInfo.IgdbId);

            await workerRepository.FinalizeGameEnrichmentAsync(update, stoppingToken);

            if (!string.IsNullOrWhiteSpace(update.IgdbId))
            {
                var streamerIds = await workerRepository.GetStreamerIdsByGameIdAsync(item.GameId, stoppingToken);
                await streamerStatsService.UpdateGamingRatioAsync(streamerIds, stoppingToken);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to enrich game {GameId}.", item.GameId);
            using var scope = _scopeFactory.CreateScope();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            await workerRepository.IncrementGameRetryAsync(item.GameId, stoppingToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
