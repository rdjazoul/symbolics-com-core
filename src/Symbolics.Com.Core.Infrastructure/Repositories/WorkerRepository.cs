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
            : new WorkerStateDto(state.WorkerName, state.CurrentCursor, state.LastCleanupDate, state.IsEnabled);
    }

    public async Task<IReadOnlyList<WorkerStateDto>> GetWorkerStatesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkerStates
            .AsNoTracking()
            .OrderBy(entity => entity.WorkerName)
            .Select(entity => new WorkerStateDto(
                entity.WorkerName,
                entity.CurrentCursor,
                entity.LastCleanupDate,
                entity.IsEnabled))
            .ToListAsync(cancellationToken);
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
                LastCleanupDate = state.LastCleanupDate,
                IsEnabled = state.IsEnabled
            });
        }
        else
        {
            existing.CurrentCursor = state.CurrentCursor;
            existing.LastCleanupDate = state.LastCleanupDate;
            existing.IsEnabled = state.IsEnabled;
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

    public async Task<IReadOnlyList<StreamerEnrichmentQueueItem>> GetStreamerEnrichmentQueueAsync(
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.StreamerEnrichmentQueues
            .AsNoTracking()
            .Where(entity => entity.RetryCount <= maxRetryCount)
            .Where(entity => entity.Status == EnrichmentStatus.Pending || entity.Status == EnrichmentStatus.RetryDelay)
            .Join(
                _dbContext.StreamerTwitches.AsNoTracking(),
                queue => queue.StreamerId,
                twitch => twitch.StreamerId,
                (queue, twitch) => new { queue.StreamerId, twitch.TwitchLogin })
            .GroupBy(entry => entry.StreamerId)
            .Select(group => new StreamerEnrichmentQueueItem(
                group.Key,
                group.Select(entry => entry.TwitchLogin).First()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameEnrichmentQueueItem>> GetGameEnrichmentQueueAsync(
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.GameEnrichmentQueues
            .AsNoTracking()
            .Where(entity => entity.RetryCount <= maxRetryCount)
            .Where(entity => entity.Status == EnrichmentStatus.Pending || entity.Status == EnrichmentStatus.RetryDelay)
            .Join(
                _dbContext.GameTwitches.AsNoTracking(),
                queue => queue.GameId,
                twitch => twitch.GameId,
                (queue, twitch) => new { queue.GameId, twitch.TwitchId })
            .GroupBy(entry => entry.GameId)
            .Select(group => new GameEnrichmentQueueItem(
                group.Key,
                group.Select(entry => entry.TwitchId).First()))
            .ToListAsync(cancellationToken);
    }

    public async Task FinalizeStreamerEnrichmentAsync(StreamerEnrichmentUpdate update, CancellationToken cancellationToken = default)
    {
        var streamer = await _dbContext.Streamers
            .SingleOrDefaultAsync(entity => entity.Id == update.StreamerId, cancellationToken);

        if (streamer is null)
        {
            streamer = new Streamer
            {
                Id = update.StreamerId
            };
            _dbContext.Streamers.Add(streamer);
        }

        streamer.VectorDescription = update.VectorDescription;
        streamer.PersonaDescription = update.PersonaDescription;
        if (!string.IsNullOrWhiteSpace(update.Email))
        {
            streamer.Email = update.Email;
        }
        streamer.Language = update.Language;
        streamer.LastModificationDate = update.LastModificationDate;
        streamer.IsReady = true;

        var twitch = await _dbContext.StreamerTwitches
            .SingleOrDefaultAsync(entity => entity.TwitchId == update.TwitchId, cancellationToken);

        if (twitch is null)
        {
            _dbContext.StreamerTwitches.Add(new StreamerTwitch
            {
                TwitchId = update.TwitchId,
                StreamerId = update.StreamerId,
                TwitchLogin = update.TwitchLogin,
                TwitchName = update.TwitchName
            });
        }
        else
        {
            twitch.StreamerId = update.StreamerId;
            twitch.TwitchLogin = update.TwitchLogin;
            twitch.TwitchName = update.TwitchName;
        }

        var queueEntry = await _dbContext.StreamerEnrichmentQueues
            .SingleOrDefaultAsync(entity => entity.StreamerId == update.StreamerId, cancellationToken);

        if (queueEntry is not null)
        {
            _dbContext.StreamerEnrichmentQueues.Remove(queueEntry);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizeGameEnrichmentAsync(GameEnrichmentUpdate update, CancellationToken cancellationToken = default)
    {
        var game = await _dbContext.Games
            .SingleOrDefaultAsync(entity => entity.Id == update.GameId, cancellationToken);

        if (game is null)
        {
            game = new Game
            {
                Id = update.GameId,
                Name = update.TwitchName
            };
            _dbContext.Games.Add(game);
        }

        game.Name = update.TwitchName;
        game.VectorDescription = update.VectorDescription;
        game.IsReady = true;

        var twitch = await _dbContext.GameTwitches
            .SingleOrDefaultAsync(entity => entity.TwitchId == update.TwitchId, cancellationToken);

        if (twitch is null)
        {
            _dbContext.GameTwitches.Add(new GameTwitch
            {
                TwitchId = update.TwitchId,
                GameId = update.GameId,
                TwitchName = update.TwitchName
            });
        }
        else
        {
            twitch.GameId = update.GameId;
            twitch.TwitchName = update.TwitchName;
        }

        var queueEntry = await _dbContext.GameEnrichmentQueues
            .SingleOrDefaultAsync(entity => entity.GameId == update.GameId, cancellationToken);

        if (queueEntry is not null)
        {
            _dbContext.GameEnrichmentQueues.Remove(queueEntry);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementStreamerRetryAsync(Guid streamerId, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.StreamerEnrichmentQueues
            .SingleOrDefaultAsync(entity => entity.StreamerId == streamerId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        entry.RetryCount += 1;
        entry.Status = EnrichmentStatus.RetryDelay;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementGameRetryAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.GameEnrichmentQueues
            .SingleOrDefaultAsync(entity => entity.GameId == gameId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        entry.RetryCount += 1;
        entry.Status = EnrichmentStatus.RetryDelay;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureConnectionOpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await ((DbConnection)connection).OpenAsync(cancellationToken);
        }
    }
}
