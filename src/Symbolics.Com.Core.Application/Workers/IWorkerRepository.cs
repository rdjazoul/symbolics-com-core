namespace Symbolics.Com.Core.Application.Workers;

public interface IWorkerRepository
{
    Task<WorkerStateDto?> GetWorkerStateAsync(string workerName, CancellationToken cancellationToken = default);
    Task UpdateWorkerStateAsync(WorkerStateDto state, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(string workerName, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string workerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, Guid>> GetStreamerIdsByTwitchIdsAsync(IEnumerable<string> twitchIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, Guid>> GetGameIdsByTwitchIdsAsync(IEnumerable<string> twitchIds, CancellationToken cancellationToken = default);
    Task AddStreamersAsync(IEnumerable<StreamerCreation> streamers, CancellationToken cancellationToken = default);
    Task AddGamesAsync(IEnumerable<GameCreation> games, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetExistingStreamIdsAsync(IEnumerable<string> streamIds, CancellationToken cancellationToken = default);
    Task AddGamePlayedBatchAsync(IEnumerable<GamePlayedCreation> gamePlays, CancellationToken cancellationToken = default);
}
