using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class RecommendationRepository(CoreDbContext dbContext) : IRecommendationRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<StreamerGameRow>> GetStreamerGameRowsAsync(
        IReadOnlyCollection<Guid> gameIds,
        string? language,
        CancellationToken cancellationToken = default)
    {
        if (gameIds.Count == 0)
        {
            return [];
        }

        var query = from gamePlay in _dbContext.GamePlays.AsNoTracking()
                    join streamer in _dbContext.Streamers.AsNoTracking()
                        on gamePlay.StreamerId equals streamer.Id
                    join twitch in _dbContext.StreamerTwitches.AsNoTracking()
                        on streamer.Id equals twitch.StreamerId
                    where gameIds.Contains(gamePlay.GameId)
                    select new { gamePlay, streamer, twitch };

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(entry => entry.streamer.Language == language);
        }

        return await query
            .Select(entry => new StreamerGameRow(
                entry.streamer.Id,
                entry.streamer.Language,
                entry.streamer.Email != null,
                entry.twitch.TwitchId,
                entry.twitch.TwitchLogin,
                entry.twitch.TwitchName,
                entry.gamePlay.GameId))
            .ToListAsync(cancellationToken);
    }
}
