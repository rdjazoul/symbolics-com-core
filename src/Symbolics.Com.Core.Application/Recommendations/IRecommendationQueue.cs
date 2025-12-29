namespace Symbolics.Com.Core.Application.Recommendations;

public interface IRecommendationQueue
{
    ValueTask EnqueueAsync(RecommendationQueueItem item, CancellationToken cancellationToken = default);
    ValueTask<RecommendationQueueItem> DequeueAsync(CancellationToken cancellationToken = default);
}
