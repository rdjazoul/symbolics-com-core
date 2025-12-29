using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Recommendations;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class RecommendationWorker(
    IServiceScopeFactory scopeFactory,
    IRecommendationQueue queue,
    ILogger<RecommendationWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IRecommendationQueue _queue = queue;
    private readonly ILogger<RecommendationWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            RecommendationQueueItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IRecommendationProcessor>();
                var cache = scope.ServiceProvider.GetRequiredService<IRecommendationCache>();
                var results = await processor.ProcessAsync(item, stoppingToken);
                await cache.SetCompletedAsync(item.SearchId, results, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to process recommendations for {SearchId}.", item.SearchId);
                using var scope = _scopeFactory.CreateScope();
                var cache = scope.ServiceProvider.GetRequiredService<IRecommendationCache>();
                await cache.SetCompletedAsync(item.SearchId, Array.Empty<MatchResult>(), stoppingToken);
            }
        }
    }
}
