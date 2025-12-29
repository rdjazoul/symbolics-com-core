namespace Symbolics.Com.Core.Application.Services;

public interface IRecommendationQueue
{
    ValueTask QueueAsync(RecommendationJob job, CancellationToken cancellationToken = default);
    ValueTask<RecommendationJob> DequeueAsync(CancellationToken cancellationToken = default);
}
