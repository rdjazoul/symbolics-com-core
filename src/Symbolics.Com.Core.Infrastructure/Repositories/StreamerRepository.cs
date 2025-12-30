using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class StreamerRepository(CoreDbContext dbContext) : IStreamerRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task<PagedResult<StreamerListingRow>> GetStreamersAsync(
        string? language,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = from gamePlay in _dbContext.GamePlays.AsNoTracking()
                        join streamer in _dbContext.Streamers.AsNoTracking()
                            on gamePlay.StreamerId equals streamer.Id
                        join twitch in _dbContext.StreamerTwitches.AsNoTracking()
                            on streamer.Id equals twitch.StreamerId
                        select new
                        {
                            streamer.Id,
                            streamer.Email,
                            twitch.TwitchId,
                            twitch.TwitchLogin,
                            twitch.TwitchName,
                            gamePlay.Language
                        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            baseQuery = baseQuery.Where(entry => entry.Language == language);
        }

        var distinctQuery = baseQuery
            .GroupBy(entry => new
            {
                entry.Id,
                entry.Email,
                entry.TwitchId,
                entry.TwitchLogin,
                entry.TwitchName,
                entry.Language
            })
            .Select(group => new StreamerListingRow(
                group.Key.Id,
                group.Key.TwitchId,
                group.Key.Language,
                !string.IsNullOrWhiteSpace(group.Key.Email),
                group.Key.TwitchLogin,
                group.Key.TwitchName));

        var totalCount = await distinctQuery.CountAsync(cancellationToken);
        var items = await distinctQuery
            .OrderBy(entry => entry.StreamerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StreamerListingRow>(items, totalCount);
    }
}
