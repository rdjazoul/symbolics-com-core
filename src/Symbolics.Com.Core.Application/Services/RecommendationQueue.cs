using System.Threading.Channels;

namespace Symbolics.Com.Core.Application.Services;

public sealed class RecommendationQueue : IRecommendationQueue
{
    private readonly Channel<RecommendationJob> _channel = Channel.CreateUnbounded<RecommendationJob>();

    public ValueTask QueueAsync(RecommendationJob job, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<RecommendationJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
