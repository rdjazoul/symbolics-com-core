namespace Symbolics.Com.Core.Application.Recommendations;

public interface IRecommendationProcessor
{
    Task<IReadOnlyList<MatchResult>> ProcessAsync(
        RecommendationQueueItem item,
        CancellationToken cancellationToken = default);
}
