using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/admin/recommendations")]
[ApiKey(ApiKeyScope.Admin)]
public sealed class AdminRecommendationsController(
    IEmbeddingService embeddingService,
    IRecommendationService recommendationService,
    IStreamerRepository streamerRepository,
    IAirtableService airtableService,
    ILogger<AdminRecommendationsController> logger) : ControllerBase
{
    private const int MinimumDescriptionLength = 10;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IRecommendationService _recommendationService = recommendationService;
    private readonly IStreamerRepository _streamerRepository = streamerRepository;
    private readonly IAirtableService _airtableService = airtableService;
    private readonly ILogger<AdminRecommendationsController> _logger = logger;

    [HttpPost("express")]
    [ProducesResponseType(typeof(AdminRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitExpressRecommendation(
        [FromBody] AdminExpressRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length < MinimumDescriptionLength)
        {
            return BadRequest($"Description must be at least {MinimumDescriptionLength} characters long.");
        }

        var embedding = await _embeddingService.GenerateEmbedding(description);
        var recommendations = await _recommendationService.BuildRecommendationsAsync(
            embedding.Vector,
            request.Language,
            cancellationToken);

        if (recommendations.Count == 0)
        {
            return Ok(new AdminRecommendationResponse(0, Array.Empty<MatchResult>()));
        }

        var candidates = recommendations
            .Where(result => result.HasEmail)
            .ToList();

        if (candidates.Count == 0)
        {
            return Ok(new AdminRecommendationResponse(0, Array.Empty<MatchResult>()));
        }

        var streamerIds = candidates
            .Select(result => result.Id)
            .Distinct()
            .ToArray();
        var streamerDetails = await _streamerRepository.GetStreamerDetailsAsync(streamerIds, cancellationToken);

        var candidatePairs = candidates
            .Select(result =>
            {
                streamerDetails.TryGetValue(result.Id, out var details);
                return new { Result = result, Details = details };
            })
            .Where(pair => pair.Details is not null && !string.IsNullOrWhiteSpace(pair.Details!.Email))
            .ToList();

        if (candidatePairs.Count == 0)
        {
            return Ok(new AdminRecommendationResponse(0, Array.Empty<MatchResult>()));
        }

        var skip = Math.Max(0, request.Skip.GetValueOrDefault());
        if (skip >= candidatePairs.Count)
        {
            return Ok(new AdminRecommendationResponse(0, Array.Empty<MatchResult>()));
        }

        var limit = request.Limit.GetValueOrDefault();
        var remainingCount = candidatePairs.Count - skip;
        if (limit <= 0 || limit > remainingCount)
        {
            limit = remainingCount;
        }

        var selectedPairs = candidatePairs
            .Skip(skip)
            .Take(limit)
            .ToList();

        var successfulResults = new List<MatchResult>(selectedPairs.Count);

        foreach (var pair in selectedPairs)
        {
            var record = new AirtableRecommendationRecord(
                pair.Result.TwitchName,
                pair.Result.UrlTwitch,
                pair.Details!.VectorDescription ?? string.Empty,
                pair.Details.PersonaDescription ?? string.Empty,
                pair.Result.Language ?? string.Empty,
                pair.Details.Email!,
                false);

            try
            {
                await _airtableService.CreateRecommendationsAsync(new[] { record }, cancellationToken);
                successfulResults.Add(pair.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send Airtable recommendation for streamer {StreamerId} ({TwitchName}).",
                    pair.Result.Id,
                    pair.Result.TwitchName);
            }
        }

        if (successfulResults.Count == 0)
        {
            return Ok(new AdminRecommendationResponse(0, Array.Empty<MatchResult>()));
        }

        return Ok(new AdminRecommendationResponse(successfulResults.Count, successfulResults));
    }
}
