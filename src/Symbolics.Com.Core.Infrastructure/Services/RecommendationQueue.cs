using System.Threading.Channels;
using Symbolics.Com.Core.Application.Recommendations;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class RecommendationQueue : IRecommendationQueue
{
    private readonly Channel<RecommendationQueueItem> _channel = Channel.CreateUnbounded<RecommendationQueueItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(RecommendationQueueItem item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public ValueTask<RecommendationQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
