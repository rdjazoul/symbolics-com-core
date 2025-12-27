using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Infrastructure.Entities;
using Symbolics.Com.Core.Infrastructure.Persistence;

namespace Symbolics.Com.Core.Infrastructure.Repositories;

public sealed class WorkerRepository(CoreDbContext dbContext) : IWorkerRepository
{
    private readonly CoreDbContext _dbContext = dbContext;

    public async Task<WorkerStateDto?> GetWorkerStateAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.WorkerStates
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.WorkerName == workerName, cancellationToken);

        return state is null
            ? null
            : new WorkerStateDto(state.WorkerName, state.CurrentCursor, state.LastCleanupDate);
    }

    public async Task UpdateWorkerStateAsync(WorkerStateDto state, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.WorkerStates
            .SingleOrDefaultAsync(entity => entity.WorkerName == state.WorkerName, cancellationToken);

        if (existing is null)
        {
            _dbContext.WorkerStates.Add(new WorkerState
            {
                WorkerName = state.WorkerName,
                CurrentCursor = state.CurrentCursor,
                LastCleanupDate = state.LastCleanupDate
            });
        }
        else
        {
            existing.CurrentCursor = state.CurrentCursor;
            existing.LastCleanupDate = state.LastCleanupDate;
            _dbContext.WorkerStates.Update(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAcquireLockAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtext(@workerName));";
        command.Parameters.Add(new NpgsqlParameter("workerName", workerName));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool locked && locked;
    }

    public async Task ReleaseLockAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await EnsureConnectionOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(hashtext(@workerName));";
        command.Parameters.Add(new NpgsqlParameter("workerName", workerName));
        await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetStreamerIdsByTwitchIdsAsync(
        IEnumerable<string> twitchIds,
        CancellationToken cancellationToken = default)
    {
        var ids = twitchIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, Guid>();
        }

        return await _dbContext.StreamerTwitches
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.TwitchId))
            .ToDictionaryAsync(entity => entity.TwitchId, entity => entity.StreamerId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetGameIdsByTwitchIdsAsync(
        IEnumerable<string> twitchIds,
        CancellationToken cancellationToken = default)
    {
        var ids = twitchIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, Guid>();
        }

        return await _dbContext.GameTwitches
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.TwitchId))
            .ToDictionaryAsync(entity => entity.TwitchId, entity => entity.GameId, cancellationToken);
    }

    public async Task AddStreamersAsync(IEnumerable<StreamerCreation> streamers, CancellationToken cancellationToken = default)
    {
        var entries = streamers.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"Streamer\" (\"Id\", \"LastModificationDate\") VALUES ",
            entries.Select(entry => new object[] { entry.StreamerId, now }),
            " ON CONFLICT (\"Id\") DO NOTHING;",
            cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"StreamerTwitch\" (\"TwitchId\", \"StreamerId\", \"TwitchLogin\", \"TwitchName\") VALUES ",
            entries.Select(entry => new object[] { entry.TwitchId, entry.StreamerId, entry.TwitchLogin, entry.TwitchName }),
            " ON CONFLICT (\"TwitchId\") DO NOTHING;",
            cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"StreamerEnrichmentQueue\" (\"StreamerId\", \"Status\", \"RetryCount\", \"AddedAt\") VALUES ",
            entries.Select(entry => new object[] { entry.StreamerId, EnrichmentStatus.Pending, 0, now }),
            " ON CONFLICT (\"StreamerId\") DO NOTHING;",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddGamesAsync(IEnumerable<GameCreation> games, CancellationToken cancellationToken = default)
    {
        var entries = games.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"Game\" (\"Id\", \"Name\") VALUES ",
            entries.Select(entry => new object[] { entry.GameId, entry.GameName }),
            " ON CONFLICT (\"Id\") DO NOTHING;",
            cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"GameTwitch\" (\"TwitchId\", \"GameId\", \"TwitchName\") VALUES ",
            entries.Select(entry => new object[] { entry.TwitchId, entry.GameId, entry.TwitchName }),
            " ON CONFLICT (\"TwitchId\") DO NOTHING;",
            cancellationToken);

        await ExecuteBatchAsync(
            "INSERT INTO \"GameEnrichmentQueue\" (\"GameId\", \"Status\", \"RetryCount\", \"AddedAt\") VALUES ",
            entries.Select(entry => new object[] { entry.GameId, EnrichmentStatus.Pending, 0, now }),
            " ON CONFLICT (\"GameId\") DO NOTHING;",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetExistingStreamIdsAsync(
        IEnumerable<string> streamIds,
        CancellationToken cancellationToken = default)
    {
        var ids = streamIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>();
        }

        var found = await _dbContext.GamePlays
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.TwitchStreamId))
            .Select(entity => entity.TwitchStreamId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public async Task AddGamePlayedBatchAsync(IEnumerable<GamePlayedCreation> gamePlays, CancellationToken cancellationToken = default)
    {
        var entries = gamePlays.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        await ExecuteBatchAsync(
            "INSERT INTO \"GamePlayed\" (\"Id\", \"StreamerId\", \"GameId\", \"ViewerCount\", \"Date\", \"Language\", \"TwitchStreamId\") VALUES ",
            entries.Select(entry => new object[]
            {
                entry.GamePlayedId,
                entry.StreamerId,
                entry.GameId,
                entry.ViewerCount,
                entry.Date,
                entry.Language,
                entry.TwitchStreamId
            }),
            " ON CONFLICT (\"TwitchStreamId\") DO NOTHING;",
            cancellationToken);
    }

    private async Task ExecuteBatchAsync(
        string sqlPrefix,
        IEnumerable<object[]> values,
        string sqlSuffix,
        CancellationToken cancellationToken)
    {
        var parameterIndex = 0;
        var parameters = new List<NpgsqlParameter>();
        var rows = new List<string>();

        foreach (var rowValues in values)
        {
            var placeholders = new List<string>();
            foreach (var value in rowValues)
            {
                var parameterName = $"p{parameterIndex++}";
                placeholders.Add($"@{parameterName}");
                parameters.Add(new NpgsqlParameter(parameterName, value ?? DBNull.Value));
            }

            rows.Add($"({string.Join(", ", placeholders)})");
        }

        if (rows.Count == 0)
        {
            return;
        }

        var sql = $"{sqlPrefix}{string.Join(", ", rows)}{sqlSuffix}";
        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters.ToArray(), cancellationToken);
    }

    private static async Task EnsureConnectionOpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await ((DbConnection)connection).OpenAsync(cancellationToken);
        }
    }
}
