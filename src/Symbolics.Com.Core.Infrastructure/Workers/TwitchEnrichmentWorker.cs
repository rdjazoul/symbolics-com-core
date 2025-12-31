using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;
using Symbolics.Com.Core.Infrastructure.Services;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchEnrichmentWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TwitchEnrichmentOptions> optionsMonitor,
    IOptionsMonitor<CostSettings> costSettingsMonitor,
    IDailyCostMonitor dailyCostMonitor,
    ILogger<TwitchEnrichmentWorker> logger) : BackgroundService
{
    private const string WorkerName = "Enrichment";
    private const string GenerateEmbeddingAction = "GenerateEmbedding";
    private const string GenerateGameDescriptionAction = "GenerateGameDescription";
    private const string GenerateStreamerDescriptionAction = "GenerateStreamerDescription";
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IOptionsMonitor<TwitchEnrichmentOptions> _optionsMonitor = optionsMonitor;
    private readonly IOptionsMonitor<CostSettings> _costSettingsMonitor = costSettingsMonitor;
    private readonly IDailyCostMonitor _dailyCostMonitor = dailyCostMonitor;
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

        using var scope = _scopeFactory.CreateScope();
        var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();

        var costSettings = _costSettingsMonitor.CurrentValue;
        var totalCost = await _dailyCostMonitor.GetCurrentCostAsync(DateTime.UtcNow.Date, stoppingToken);
        if (totalCost >= costSettings.DailyCostThreshold)
        {
            _logger.LogWarning(
                "Daily AI cost threshold reached. TotalCost={TotalCost} Threshold={DailyCostThreshold}",
                totalCost,
                costSettings.DailyCostThreshold);
            return;
        }

        var options = _optionsMonitor.CurrentValue;
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
        var budgetState = new BudgetState();

        var streamerTasks = streamerQueue.Select(item => ProcessStreamerAsync(item, budgetState, semaphore, stoppingToken));
        var gameTasks = gameQueue.Select(item => ProcessGameAsync(item, budgetState, semaphore, stoppingToken));

        await Task.WhenAll(streamerTasks.Concat(gameTasks));
    }

    private async Task ProcessStreamerAsync(
        StreamerEnrichmentQueueItem item,
        BudgetState budgetState,
        SemaphoreSlim semaphore,
        CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            if (await ShouldStopForBudgetAsync(budgetState, stoppingToken))
            {
                return;
            }

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
                GenerateStreamerDescriptionAction,
                aiDescriptions.Consumption.Model,
                aiDescriptions.Consumption.InputUnits,
                aiDescriptions.Consumption.OutputUnits,
                aiDescriptions.Consumption.CachedUnits,
                aiDescriptions.Consumption.ProcessingTimeMs);
            _dailyCostMonitor.RegisterConsumption(GenerateStreamerDescriptionAction, aiDescriptions.Consumption);

            if (await ShouldStopForBudgetAsync(budgetState, stoppingToken))
            {
                return;
            }

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            await consumptionTracker.LogAsync(
                "Gemini",
                GenerateEmbeddingAction,
                embedding.Consumption.Model,
                embedding.Consumption.InputUnits,
                embedding.Consumption.OutputUnits,
                embedding.Consumption.CachedUnits,
                embedding.Consumption.ProcessingTimeMs);
            _dailyCostMonitor.RegisterConsumption(GenerateEmbeddingAction, embedding.Consumption);
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
        BudgetState budgetState,
        SemaphoreSlim semaphore,
        CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);
        try
        {
            if (await ShouldStopForBudgetAsync(budgetState, stoppingToken))
            {
                return;
            }

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
                GenerateGameDescriptionAction,
                aiDescriptions.Consumption.Model,
                aiDescriptions.Consumption.InputUnits,
                aiDescriptions.Consumption.OutputUnits,
                aiDescriptions.Consumption.CachedUnits,
                aiDescriptions.Consumption.ProcessingTimeMs);
            _dailyCostMonitor.RegisterConsumption(GenerateGameDescriptionAction, aiDescriptions.Consumption);

            if (await ShouldStopForBudgetAsync(budgetState, stoppingToken))
            {
                return;
            }

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            await consumptionTracker.LogAsync(
                "Gemini",
                GenerateEmbeddingAction,
                embedding.Consumption.Model,
                embedding.Consumption.InputUnits,
                embedding.Consumption.OutputUnits,
                embedding.Consumption.CachedUnits,
                embedding.Consumption.ProcessingTimeMs);
            _dailyCostMonitor.RegisterConsumption(GenerateEmbeddingAction, embedding.Consumption);
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

    private async Task<bool> ShouldStopForBudgetAsync(BudgetState budgetState, CancellationToken stoppingToken)
    {
        if (budgetState.IsStopped)
        {
            return true;
        }

        var currentCost = await _dailyCostMonitor.GetCurrentCostAsync(DateTime.UtcNow.Date, stoppingToken);
        var threshold = _costSettingsMonitor.CurrentValue.DailyCostThreshold;
        if (currentCost >= threshold)
        {
            if (budgetState.TryStop())
            {
                _logger.LogWarning(
                    "Batch processing interrupted: Daily budget reached mid-cycle ({CurrentCost}$ / {Threshold}$)",
                    currentCost,
                    threshold);
            }

            return true;
        }

        return false;
    }

    private sealed class BudgetState
    {
        private int _stopped;

        public bool IsStopped => Volatile.Read(ref _stopped) == 1;

        public bool TryStop() => Interlocked.Exchange(ref _stopped, 1) == 0;
    }
}
