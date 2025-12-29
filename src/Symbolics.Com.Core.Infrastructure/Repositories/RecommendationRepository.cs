using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Recommendations;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class RecommendationRepository(CoreDbContext dbContext) : IRecommendationRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<StreamerMatchData>> GetStreamersByGamesAsync(
        IReadOnlyCollection<Guid> gameIds,
        string? language,
        CancellationToken cancellationToken = default)
    {
        if (gameIds.Count == 0)
        {
            return Array.Empty<StreamerMatchData>();
        }

        var query =
            from played in _dbContext.GamePlays.AsNoTracking()
            join streamer in _dbContext.Streamers.AsNoTracking()
                on played.StreamerId equals streamer.Id
            join twitch in _dbContext.StreamerTwitches.AsNoTracking()
                on streamer.Id equals twitch.StreamerId
            where gameIds.Contains(played.GameId)
            select new
            {
                streamer.Id,
                streamer.Language,
                streamer.Email,
                twitch.TwitchId,
                twitch.TwitchLogin,
                twitch.TwitchName,
                played.GameId
            };

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(entry => entry.Language == language);
        }

        var grouped = await query
            .GroupBy(entry => new
            {
                entry.Id,
                entry.Language,
                entry.Email,
                entry.TwitchId,
                entry.TwitchLogin,
                entry.TwitchName
            })
            .Select(group => new StreamerMatchData(
                group.Key.Id,
                group.Key.TwitchId,
                group.Key.TwitchLogin,
                group.Key.TwitchName,
                group.Key.Language,
                !string.IsNullOrWhiteSpace(group.Key.Email),
                group.Select(entry => entry.GameId).Distinct().ToArray()))
            .ToListAsync(cancellationToken);

        return grouped;
    }
}
