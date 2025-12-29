using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[ApiKey(ApiKeyScope.Service)]
public sealed class RecommendationsController(
    IRecommendationQueue recommendationQueue,
    IDistributedCache cache,
    IOptionsMonitor<RecommendationOptions> optionsMonitor,
    ILogger<RecommendationsController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IRecommendationQueue _recommendationQueue = recommendationQueue;
    private readonly IDistributedCache _cache = cache;
    private readonly IOptionsMonitor<RecommendationOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<RecommendationsController> _logger = logger;

    [HttpPost]
    [ProducesResponseType(typeof(RecommendationAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitRecommendation(
        [FromBody] RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Vector is null || request.Vector.Length == 0)
        {
            return BadRequest("Vector is required.");
        }

        var searchId = Guid.NewGuid();
        var entry = new RecommendationCacheEntry(RecommendationStatus.Processing, null);
        await SetCacheAsync(searchId, entry, cancellationToken);

        await _recommendationQueue.QueueAsync(
            new RecommendationJob(searchId, request.Vector, request.Language),
            cancellationToken);

        return Accepted(new RecommendationAcceptedResponse(searchId));
    }

    [HttpGet("{searchId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<MatchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RecommendationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRecommendation(Guid searchId, CancellationToken cancellationToken)
    {
        var payload = await _cache.GetStringAsync(BuildCacheKey(searchId), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return NotFound();
        }

        RecommendationCacheEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<RecommendationCacheEntry>(payload, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize recommendation cache entry {SearchId}.", searchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Invalid cache entry.");
        }

        if (entry is null)
        {
            return NotFound();
        }

        if (entry.Status == RecommendationStatus.Completed && entry.Results is not null)
        {
            await SetCacheAsync(searchId, entry, cancellationToken);
            return Ok(entry.Results);
        }

        if (entry.Status == RecommendationStatus.Failed)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Recommendation processing failed.");
        }

        return Ok(new RecommendationStatusResponse(RecommendationStatus.Processing));
    }

    private Task SetCacheAsync(Guid searchId, RecommendationCacheEntry entry, CancellationToken cancellationToken)
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

        return _cache.SetStringAsync(BuildCacheKey(searchId), payload, cacheOptions, cancellationToken);
    }

    private static string BuildCacheKey(Guid searchId) => $"recommendations:{searchId}";
}
