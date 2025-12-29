namespace Symbolics.Com.Core.Application.Recommendations;

public interface IRecommendationCache
{
    Task SetProcessingAsync(Guid searchId, CancellationToken cancellationToken = default);
    Task SetCompletedAsync(Guid searchId, IReadOnlyList<MatchResult> results, CancellationToken cancellationToken = default);
    Task<RecommendationCacheEntry?> GetAsync(Guid searchId, CancellationToken cancellationToken = default);
    Task RefreshAsync(Guid searchId, CancellationToken cancellationToken = default);
}
