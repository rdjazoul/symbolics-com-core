using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchEnrichmentWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TwitchEnrichmentOptions> optionsMonitor,
    ILogger<TwitchEnrichmentWorker> logger) : BackgroundService
{
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
            var qdrantClient = scope.ServiceProvider.GetRequiredService<IQdrantClient>();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();

            var twitchInfo = await twitchService.GetStreamerInfos(item.TwitchLogin);
            var aiDescriptions = await aiService.GenerateStreamerDescription(twitchInfo.Description, twitchInfo.Login);

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            if (!qdrantClient.SaveStreamerDescription(item.StreamerId, embedding.Vector))
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
            var qdrantClient = scope.ServiceProvider.GetRequiredService<IQdrantClient>();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();

            var twitchInfo = await twitchService.GetGameInfos(item.TwitchId);
            var aiDescriptions = await aiService.GenerateGameDescription(twitchInfo.Name);

            var embedding = await embeddingService.GenerateEmbedding(aiDescriptions.Description);
            if (!qdrantClient.SaveGameDescription(item.GameId, embedding.Vector))
            {
                throw new InvalidOperationException($"Failed to save game embedding for {item.GameId}.");
            }

            var update = new GameEnrichmentUpdate(
                item.GameId,
                twitchInfo.Id,
                twitchInfo.Name,
                aiDescriptions.Description);

            await workerRepository.FinalizeGameEnrichmentAsync(update, stoppingToken);
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
