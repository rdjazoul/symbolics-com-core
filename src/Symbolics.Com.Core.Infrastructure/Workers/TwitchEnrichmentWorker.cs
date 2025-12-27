using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchEnrichmentWorker(
    ITwitchService twitchService,
    IAiService aiService,
    IEmbeddingService embeddingService,
    IQdrantClient qdrantClient,
    IWorkerRepository workerRepository,
    IOptionsMonitor<TwitchEnrichmentOptions> optionsMonitor,
    ILogger<TwitchEnrichmentWorker> logger) : BackgroundService
{
    private readonly ITwitchService _twitchService = twitchService;
    private readonly IAiService _aiService = aiService;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IQdrantClient _qdrantClient = qdrantClient;
    private readonly IWorkerRepository _workerRepository = workerRepository;
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
        var streamerQueue = await _workerRepository.GetStreamerEnrichmentQueueAsync(options.StreamerMaxRetryCount, stoppingToken);
        var gameQueue = await _workerRepository.GetGameEnrichmentQueueAsync(options.GameMaxRetryCount, stoppingToken);

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
            var twitchInfo = await _twitchService.GetStreamerInfos(item.TwitchLogin);
            var aiDescriptions = await _aiService.GenerateStreamerDescription(twitchInfo.Description, twitchInfo.Login);

            var update = new StreamerEnrichmentUpdate(
                item.StreamerId,
                twitchInfo.Id,
                twitchInfo.Login,
                twitchInfo.DisplayName,
                aiDescriptions.VectorDescription,
                aiDescriptions.PersonaDescription,
                DateTime.UtcNow);

            await _workerRepository.UpdateStreamerEnrichmentAsync(update, stoppingToken);

            var embedding = await _embeddingService.GenerateEmbedding(aiDescriptions.VectorDescription);
            if (!_qdrantClient.SaveStreamerDescription(item.StreamerId, embedding.Vector))
            {
                throw new InvalidOperationException($"Failed to save streamer embedding for {item.StreamerId}.");
            }

            await _workerRepository.RemoveStreamerFromEnrichmentQueueAsync(item.StreamerId, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to enrich streamer {StreamerId}.", item.StreamerId);
            await _workerRepository.IncrementStreamerRetryAsync(item.StreamerId, stoppingToken);
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
            var twitchInfo = await _twitchService.GetGameInfos(item.TwitchId);
            var aiDescriptions = await _aiService.GenerateGameDescription(twitchInfo.Name);

            var update = new GameEnrichmentUpdate(
                item.GameId,
                twitchInfo.Id,
                twitchInfo.Name,
                aiDescriptions.Description);

            await _workerRepository.UpdateGameEnrichmentAsync(update, stoppingToken);

            var embedding = await _embeddingService.GenerateEmbedding(aiDescriptions.Description);
            if (!_qdrantClient.SaveGameDescription(item.GameId, embedding.Vector))
            {
                throw new InvalidOperationException($"Failed to save game embedding for {item.GameId}.");
            }

            await _workerRepository.RemoveGameFromEnrichmentQueueAsync(item.GameId, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Failed to enrich game {GameId}.", item.GameId);
            await _workerRepository.IncrementGameRetryAsync(item.GameId, stoppingToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
