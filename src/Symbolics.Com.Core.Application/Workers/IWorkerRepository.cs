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
    Task<IReadOnlyList<StreamerEnrichmentQueueItem>> GetStreamerEnrichmentQueueAsync(
        int maxRetryCount,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameEnrichmentQueueItem>> GetGameEnrichmentQueueAsync(
        int maxRetryCount,
        CancellationToken cancellationToken = default);
    Task UpdateStreamerEnrichmentAsync(StreamerEnrichmentUpdate update, CancellationToken cancellationToken = default);
    Task UpdateGameEnrichmentAsync(GameEnrichmentUpdate update, CancellationToken cancellationToken = default);
    Task RemoveStreamerFromEnrichmentQueueAsync(Guid streamerId, CancellationToken cancellationToken = default);
    Task RemoveGameFromEnrichmentQueueAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task IncrementStreamerRetryAsync(Guid streamerId, CancellationToken cancellationToken = default);
    Task IncrementGameRetryAsync(Guid gameId, CancellationToken cancellationToken = default);
}
