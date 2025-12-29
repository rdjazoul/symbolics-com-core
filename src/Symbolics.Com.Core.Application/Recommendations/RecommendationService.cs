namespace Symbolics.Com.Core.Application.Recommendations;

public sealed class RecommendationService(
    IRecommendationQueue queue,
    IRecommendationCache cache) : IRecommendationService
{
    private readonly IRecommendationQueue _queue = queue;
    private readonly IRecommendationCache _cache = cache;

    public async Task<Guid> SubmitAsync(float[] vector, string? language, CancellationToken cancellationToken = default)
    {
        var searchId = Guid.NewGuid();
        await _cache.SetProcessingAsync(searchId, cancellationToken);
        await _queue.EnqueueAsync(new RecommendationQueueItem(searchId, vector, language), cancellationToken);
        return searchId;
    }

    public async Task<RecommendationResponse?> GetAsync(Guid searchId, CancellationToken cancellationToken = default)
    {
        var entry = await _cache.GetAsync(searchId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (entry.Status == RecommendationStatus.Completed)
        {
            await _cache.RefreshAsync(searchId, cancellationToken);
        }

        return new RecommendationResponse(entry.Status, entry.Results);
    }
}
