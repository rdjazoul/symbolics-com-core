using Microsoft.AspNetCore.Mvc;
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
    IAirtableService airtableService) : ControllerBase
{
    private const int MinimumDescriptionLength = 10;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly IRecommendationService _recommendationService = recommendationService;
    private readonly IStreamerRepository _streamerRepository = streamerRepository;
    private readonly IAirtableService _airtableService = airtableService;

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
            return Ok(new AdminRecommendationResponse(0, recommendations));
        }

        var limit = request.Limit.GetValueOrDefault();
        if (limit <= 0)
        {
            limit = recommendations.Count;
        }

        var selectedRecommendations = recommendations.Take(limit).ToList();
        var streamerIds = selectedRecommendations.Select(result => result.Id).ToArray();
        var streamerDetails = await _streamerRepository.GetStreamerDetailsAsync(streamerIds, cancellationToken);
        var detailsMap = streamerDetails.ToDictionary(entry => entry.StreamerId);

        var records = selectedRecommendations
            .Select(result =>
            {
                detailsMap.TryGetValue(result.Id, out var details);
                return new AirtableRecommendationRecord(
                    result.TwitchName,
                    result.UrlTwitch,
                    details?.VectorDescription ?? string.Empty,
                    details?.PersonaDescription ?? string.Empty,
                    result.Language ?? string.Empty,
                    details?.Email ?? string.Empty,
                    false);
            })
            .ToList();

        await _airtableService.CreateRecommendationsAsync(records, cancellationToken);

        return Ok(new AdminRecommendationResponse(records.Count, selectedRecommendations));
    }
}
