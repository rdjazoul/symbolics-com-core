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
                    select new StreamerGameRow(
                        streamer.Id,
                        streamer.Language,
                        streamer.Email != null,
                        twitch.TwitchId,
                        twitch.TwitchLogin,
                        twitch.TwitchName,
                        gamePlay.GameId);

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(row => row.Language == language);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
