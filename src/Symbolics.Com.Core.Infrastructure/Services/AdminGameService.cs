using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;
using Symbolics.Com.Core.Infrastructure.Entities;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class AdminGameService(
    CoreDbContext dbContext,
    IEmbeddingService embeddingService,
    IQdrantClient qdrantClient,
    IWorkerRepository workerRepository,
    IStreamerStatsService streamerStatsService,
    ILogger<AdminGameService> logger) : IAdminGameService
{
    private readonly CoreDbContext _dbContext = dbContext;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IQdrantClient _qdrantClient = qdrantClient;
    private readonly IWorkerRepository _workerRepository = workerRepository;
    private readonly IStreamerStatsService _streamerStatsService = streamerStatsService;
    private readonly ILogger<AdminGameService> _logger = logger;

    public async Task<IReadOnlyList<GameMissingIgdbDto>> GetGamesMissingIgdbAsync(
        CancellationToken cancellationToken = default)
    {
        var query = from game in _dbContext.Games.AsNoTracking()
                    where string.IsNullOrWhiteSpace(game.IgdbId)
                    join twitch in _dbContext.GameTwitches.AsNoTracking()
                        on game.Id equals twitch.GameId into twitchGroup
                    from twitch in twitchGroup.OrderByDescending(entry => entry.TwitchId).Take(1).DefaultIfEmpty()
                    orderby game.Name
                    select new GameMissingIgdbDto(
                        game.Id,
                        twitch == null ? null : twitch.TwitchId,
                        game.Name,
                        game.VectorDescription);

        var results = await query.ToListAsync(cancellationToken);
        return results;
    }

    public async Task<AdminGameUpdateResult> UpdateGameAsync(
        Guid gameId,
        string? igdbId,
        string? manualDescription,
        CancellationToken cancellationToken = default)
    {
        var normalizedIgdbId = string.IsNullOrWhiteSpace(igdbId) ? null : igdbId.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(manualDescription) ? null : manualDescription.Trim();
        var hasIgdbUpdate = igdbId is not null;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await _dbContext.Games
            .Include(entity => entity.EnrichmentQueue)
            .SingleOrDefaultAsync(entity => entity.Id == gameId, cancellationToken);

        if (game is null)
        {
            return new AdminGameUpdateResult(true, null, null, false, false);
        }

        var oldIgdbId = game.IgdbId;
        var descriptionUpdated = false;

        if (hasIgdbUpdate)
        {
            game.IgdbId = normalizedIgdbId;
        }

        if (normalizedDescription is not null)
        {
            game.VectorDescription = normalizedDescription;
            descriptionUpdated = true;
        }

        EnsureQueueCompleted(game);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (normalizedDescription is not null)
        {
            var embedding = await _embeddingService.GenerateEmbedding(normalizedDescription);
            if (!_qdrantClient.SaveGameDescription(gameId, embedding.Vector, normalizedDescription))
            {
                throw new InvalidOperationException($"Failed to save game embedding for {gameId}.");
            }
        }

        var newIgdbId = hasIgdbUpdate ? normalizedIgdbId : oldIgdbId;
        var gamingRatioUpdated = false;
        if (hasIgdbUpdate && !string.Equals(oldIgdbId, newIgdbId, StringComparison.Ordinal))
        {
            var streamerIds = await _workerRepository.GetStreamerIdsByGameIdAsync(gameId, cancellationToken);
            await _streamerStatsService.UpdateGamingRatioAsync(streamerIds, cancellationToken);
            gamingRatioUpdated = streamerIds.Count > 0;
        }

        _logger.LogInformation(
            "Admin updated Game {GameId}: IGDB {OldIgdb} -> {NewIgdb}, Description updated: {DescriptionUpdated}",
            gameId,
            oldIgdbId,
            newIgdbId,
            descriptionUpdated);

        return new AdminGameUpdateResult(false, oldIgdbId, newIgdbId, descriptionUpdated, gamingRatioUpdated);
    }

    private void EnsureQueueCompleted(Game game)
    {
        var now = DateTime.UtcNow;
        if (game.EnrichmentQueue is null)
        {
            game.EnrichmentQueue = new GameEnrichmentQueue
            {
                GameId = game.Id,
                Status = EnrichmentStatus.Completed,
                RetryCount = 0,
                AddedAt = now,
                LastAttempt = now
            };
            _dbContext.GameEnrichmentQueues.Add(game.EnrichmentQueue);
            return;
        }

        game.EnrichmentQueue.Status = EnrichmentStatus.Completed;
        game.EnrichmentQueue.LastAttempt = now;
    }
}
