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
        var streamerQuery = _dbContext.Streamers
            .AsNoTracking()
            .Where(streamer => streamer.IsReady);

        if (!string.IsNullOrWhiteSpace(language))
        {
            streamerQuery = streamerQuery.Where(streamer => streamer.Language == language);
        }

        var query = from streamer in streamerQuery
                    join twitch in _dbContext.StreamerTwitches.AsNoTracking()
                        on streamer.Id equals twitch.StreamerId
                    select new
                    {
                        StreamerId = streamer.Id,
                        streamer.Language,
                        streamer.Email,
                        twitch.TwitchId,
                        twitch.TwitchLogin,
                        twitch.TwitchName
                    };

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        if (skip < 0)
        {
            skip = 0;
        }

        var items = await query
            .OrderBy(entry => entry.TwitchName)
            .ThenBy(entry => entry.StreamerId)
            .Skip(skip)
            .Take(pageSize)
            .Select(entry => new StreamerListingRow(
                entry.StreamerId,
                entry.TwitchId,
                entry.Language ?? string.Empty,
                entry.Email != null,
                entry.TwitchLogin,
                entry.TwitchName))
            .ToListAsync(cancellationToken);

        return new PagedResult<StreamerListingRow>(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<Guid, StreamerDetailsRow>> GetStreamerDetailsAsync(
        IReadOnlyCollection<Guid> streamerIds,
        CancellationToken cancellationToken = default)
    {
        if (streamerIds is null)
        {
            throw new ArgumentNullException(nameof(streamerIds));
        }

        var ids = streamerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, StreamerDetailsRow>();
        }

        return await _dbContext.Streamers
            .AsNoTracking()
            .Where(streamer => ids.Contains(streamer.Id))
            .Select(streamer => new StreamerDetailsRow(
                streamer.Id,
                streamer.VectorDescription,
                streamer.PersonaDescription,
                streamer.Email,
                streamer.Language))
            .ToDictionaryAsync(row => row.StreamerId, cancellationToken);
    }
}
