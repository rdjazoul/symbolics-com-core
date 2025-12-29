using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Symbolics.Com.Core.Application.Recommendations;

public sealed class RecommendationCache(IDistributedCache cache) : IRecommendationCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);
    private readonly IDistributedCache _cache = cache;

    public Task SetProcessingAsync(Guid searchId, CancellationToken cancellationToken = default)
    {
        var entry = new RecommendationCacheEntry(RecommendationStatus.Processing, Array.Empty<MatchResult>());
        return SetEntryAsync(searchId, entry, cancellationToken);
    }

    public Task SetCompletedAsync(Guid searchId, IReadOnlyList<MatchResult> results, CancellationToken cancellationToken = default)
    {
        var entry = new RecommendationCacheEntry(RecommendationStatus.Completed, results);
        return SetEntryAsync(searchId, entry, cancellationToken);
    }

    public async Task<RecommendationCacheEntry?> GetAsync(Guid searchId, CancellationToken cancellationToken = default)
    {
        var payload = await _cache.GetStringAsync(BuildCacheKey(searchId), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RecommendationCacheEntry>(payload, SerializerOptions);
    }

    public Task RefreshAsync(Guid searchId, CancellationToken cancellationToken = default)
    {
        return _cache.RefreshAsync(BuildCacheKey(searchId), cancellationToken);
    }

    private Task SetEntryAsync(Guid searchId, RecommendationCacheEntry entry, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(entry, SerializerOptions);
        return _cache.SetStringAsync(
            BuildCacheKey(searchId),
            payload,
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = SlidingExpiration
            },
            cancellationToken);
    }

    private static string BuildCacheKey(Guid searchId) => $"recommendations:{searchId}";
}
