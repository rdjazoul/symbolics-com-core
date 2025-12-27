using System.Data;
using System.Data.Common;
using EFCore.BulkExtensions;
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

        var now = DateTime.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var streamerEntities = entries.Select(entry => new Streamer
        {
            Id = entry.StreamerId,
            LastModificationDate = now
        }).ToList();

        var twitchEntities = entries.Select(entry => new StreamerTwitch
        {
            TwitchId = entry.TwitchId,
            StreamerId = entry.StreamerId,
            TwitchLogin = entry.TwitchLogin,
            TwitchName = entry.TwitchName
        }).ToList();

        var enrichmentEntities = entries.Select(entry => new StreamerEnrichmentQueue
        {
            StreamerId = entry.StreamerId,
            Status = EnrichmentStatus.Pending,
            RetryCount = 0,
            AddedAt = now
        }).ToList();

        await _dbContext.BulkInsertAsync(
            streamerEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

        await _dbContext.BulkInsertAsync(
            twitchEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

        await _dbContext.BulkInsertAsync(
            enrichmentEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

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

        var gameEntities = entries.Select(entry => new Game
        {
            Id = entry.GameId,
            Name = entry.GameName
        }).ToList();

        var twitchEntities = entries.Select(entry => new GameTwitch
        {
            TwitchId = entry.TwitchId,
            GameId = entry.GameId,
            TwitchName = entry.TwitchName
        }).ToList();

        var enrichmentEntities = entries.Select(entry => new GameEnrichmentQueue
        {
            GameId = entry.GameId,
            Status = EnrichmentStatus.Pending,
            RetryCount = 0,
            AddedAt = now
        }).ToList();

        await _dbContext.BulkInsertAsync(
            gameEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

        await _dbContext.BulkInsertAsync(
            twitchEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

        await _dbContext.BulkInsertAsync(
            enrichmentEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);

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

        var gamePlayEntities = entries.Select(entry => new GamePlayed
        {
            Id = entry.GamePlayedId,
            StreamerId = entry.StreamerId,
            GameId = entry.GameId,
            ViewerCount = entry.ViewerCount,
            Date = entry.Date,
            Language = entry.Language,
            TwitchStreamId = entry.TwitchStreamId
        }).ToList();

        await _dbContext.BulkInsertAsync(
            gamePlayEntities,
            new BulkConfig { PreserveInsertOrder = true, SetOutputIdentity = true },
            cancellationToken: cancellationToken);
    }

    private static async Task EnsureConnectionOpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await ((DbConnection)connection).OpenAsync(cancellationToken);
        }
    }
}
