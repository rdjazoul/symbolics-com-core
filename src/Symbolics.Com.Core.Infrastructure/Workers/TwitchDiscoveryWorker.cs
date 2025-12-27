using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchDiscoveryWorker(
    ITwitchService twitchService,
    IWorkerRepository workerRepository,
    IStreamMaintenanceService streamMaintenanceService,
    IOptionsMonitor<TwitchDiscoveryOptions> optionsMonitor,
    ILogger<TwitchDiscoveryWorker> logger) : BackgroundService
{
    private const string WorkerName = "TwitchCrawler";
    private readonly ITwitchService _twitchService = twitchService;
    private readonly IWorkerRepository _workerRepository = workerRepository;
    private readonly IStreamMaintenanceService _streamMaintenanceService = streamMaintenanceService;
    private readonly IOptionsMonitor<TwitchDiscoveryOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<TwitchDiscoveryWorker> _logger = logger;

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
        _logger.LogInformation("Start new Twitch discovery Cycle");

        var options = _optionsMonitor.CurrentValue;
        if (!await _workerRepository.TryAcquireLockAsync(WorkerName, stoppingToken))
        {
            _logger.LogInformation("Twitch discovery worker is locked. Skipping this cycle.");
            return;
        }

        try
        {
            var workerState = await _workerRepository.GetWorkerStateAsync(WorkerName, stoppingToken)
                ?? new WorkerStateDto(WorkerName, null, DateTime.UtcNow);

            var response = await GetStreamsWithRetryAsync(workerState.CurrentCursor, stoppingToken);

            await ProcessStreamsAsync(response, workerState, stoppingToken);
        }
        finally
        {
            await _workerRepository.ReleaseLockAsync(WorkerName, stoppingToken);
        }
    }

    private async Task<TwitchStreamResponse> GetStreamsWithRetryAsync(string? cursor, CancellationToken stoppingToken)
    {
        var options = _optionsMonitor.CurrentValue;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= options.MaxRetryAttempts; attempt++)
        {
            try
            {
                return await _twitchService.GetStreams(cursor);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to fetch Twitch streams (attempt {Attempt}/{MaxAttempts}).", attempt, options.MaxRetryAttempts);
                if (attempt < options.MaxRetryAttempts)
                {
                    await Task.Delay(options.RetryDelay, stoppingToken);
                }
            }
        }

        throw new InvalidOperationException("Unable to fetch Twitch streams after retry attempts.", lastException);
    }

    private async Task ProcessStreamsAsync(
        TwitchStreamResponse response,
        WorkerStateDto workerState,
        CancellationToken stoppingToken)
    {
        var streams = response.Data;
        if (streams.Count == 0)
        {
            await HandleCleanupAsync(workerState with { CurrentCursor = response.Cursor }, stoppingToken);
            return;
        }

        var streamIds = streams.Select(stream => stream.Id).ToArray();
        var existingStreamIds = await _workerRepository.GetExistingStreamIdsAsync(streamIds, stoppingToken);
        var newStreams = streams.Where(stream => !existingStreamIds.Contains(stream.Id)).ToList();

        if (newStreams.Count == 0)
        {
            await HandleCleanupAsync(workerState with { CurrentCursor = response.Cursor }, stoppingToken);
            return;
        }

        var streamerIds = newStreams.Select(stream => stream.UserId).Distinct().ToArray();
        var gameIds = newStreams.Select(stream => stream.GameId).Distinct().ToArray();

        var existingStreamers = (await _workerRepository.GetStreamerIdsByTwitchIdsAsync(streamerIds, stoppingToken))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var existingGames = (await _workerRepository.GetGameIdsByTwitchIdsAsync(gameIds, stoppingToken))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        var missingStreamers = newStreams
            .Where(stream => !existingStreamers.ContainsKey(stream.UserId))
            .GroupBy(stream => stream.UserId)
            .Select(group =>
            {
                var stream = group.First();
                return new StreamerCreation(
                    Guid.NewGuid(),
                    stream.UserId,
                    stream.UserLogin,
                    stream.UserName);
            })
            .ToList();

        if (missingStreamers.Count > 0)
        {
            await _workerRepository.AddStreamersAsync(missingStreamers, stoppingToken);
            foreach (var streamer in missingStreamers)
            {
                existingStreamers[streamer.TwitchId] = streamer.StreamerId;
            }
        }

        var missingGames = newStreams
            .Where(stream => !existingGames.ContainsKey(stream.GameId))
            .GroupBy(stream => stream.GameId)
            .Select(group =>
            {
                var stream = group.First();
                return new GameCreation(
                    Guid.NewGuid(),
                    stream.GameId,
                    stream.GameName,
                    stream.GameName);
            })
            .ToList();

        if (missingGames.Count > 0)
        {
            await _workerRepository.AddGamesAsync(missingGames, stoppingToken);
            foreach (var game in missingGames)
            {
                existingGames[game.TwitchId] = game.GameId;
            }
        }

        var gamePlays = newStreams.Select(stream =>
        {
            var streamerId = existingStreamers[stream.UserId];
            var gameId = existingGames[stream.GameId];
            return new GamePlayedCreation(
                Guid.NewGuid(),
                streamerId,
                gameId,
                stream.ViewerCount,
                stream.StartedAt,
                stream.Language,
                stream.Id);
        }).ToList();

        if (gamePlays.Count > 0)
        {
            await _workerRepository.AddGamePlayedBatchAsync(gamePlays, stoppingToken);
        }

        var updatedState = workerState with { CurrentCursor = response.Cursor };
        await HandleCleanupAsync(updatedState, stoppingToken);
    }

    private async Task HandleCleanupAsync(WorkerStateDto workerState, CancellationToken stoppingToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var now = DateTime.UtcNow;
        if (now - workerState.LastCleanupDate < options.CleanupInterval)
        {
            await UpdateWorkerStateAsync(workerState, stoppingToken);
            return;
        }

        await _streamMaintenanceService.CleanStreams(stoppingToken);
        var updatedState = workerState with { LastCleanupDate = now };
        await UpdateWorkerStateAsync(updatedState, stoppingToken);
    }

    private Task UpdateWorkerStateAsync(WorkerStateDto workerState, CancellationToken stoppingToken)
    {
        return _workerRepository.UpdateWorkerStateAsync(workerState, stoppingToken);
    }
}
