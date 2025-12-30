using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class StreamerStatsService(CoreDbContext dbContext) : IStreamerStatsService
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task UpdateGamingRatioAsync(IReadOnlyCollection<Guid> streamerIds, CancellationToken cancellationToken = default)
    {
        if (streamerIds.Count == 0)
        {
            return;
        }

        var parameter = new NpgsqlParameter<Guid[]>("StreamerIds", streamerIds.ToArray())
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid
        };

        const string sql = """
        UPDATE "Streamer" s
        SET "GamingRatio" = sub.new_ratio
        FROM (
            SELECT 
                gp."StreamerId",
                COALESCE(COUNT(g."Id") FILTER (WHERE g."IgdbId" IS NOT NULL)::float / NULLIF(COUNT(gp."Id"), 0), 0) as new_ratio
            FROM "GamePlayed" gp
            INNER JOIN "Game" g ON gp."GameId" = g."Id"
            WHERE gp."StreamerId" = ANY(@StreamerIds)
            GROUP BY gp."StreamerId"
        ) AS sub
        WHERE s."Id" = sub."StreamerId";
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, [parameter], cancellationToken);
    }
}
