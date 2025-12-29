using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class RecommendationWorker(
    IRecommendationQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<RecommendationOptions> optionsMonitor,
    ILogger<RecommendationWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IRecommendationQueue _queue = queue;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IOptionsMonitor<RecommendationOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<RecommendationWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            RecommendationJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessJobAsync(job, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(RecommendationJob job, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var service = scope.ServiceProvider.GetRequiredService<IRecommendationService>();

        try
        {
            var results = await service.BuildRecommendationsAsync(job.Vector, job.Language, stoppingToken);
            var entry = new RecommendationCacheEntry(RecommendationStatus.Completed, results);
            await SetCacheAsync(cache, job.SearchId, entry, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recommendation processing failed for {SearchId}.", job.SearchId);
            var entry = new RecommendationCacheEntry(RecommendationStatus.Failed, []);
            await SetCacheAsync(cache, job.SearchId, entry, stoppingToken);
        }
    }

    private Task SetCacheAsync(
        IDistributedCache cache,
        Guid searchId,
        RecommendationCacheEntry entry,
        CancellationToken cancellationToken)
    {
        var ttlMinutes = _optionsMonitor.CurrentValue.CacheTtlMinutes;
        if (ttlMinutes <= 0)
        {
            ttlMinutes = 30;
        }

        var cacheOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(ttlMinutes)
        };
        var payload = JsonSerializer.Serialize(entry, SerializerOptions);

        return cache.SetStringAsync(BuildCacheKey(searchId), payload, cacheOptions, cancellationToken);
    }

    private static string BuildCacheKey(Guid searchId) => $"recommendations:{searchId}";
}
