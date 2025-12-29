using Microsoft.AspNetCore.Mvc;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Recommendations;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[ApiKey(ApiKeyScope.Service)]
public sealed class RecommendationsController(IRecommendationService recommendationService) : ControllerBase
{
    private readonly IRecommendationService _recommendationService = recommendationService;

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

        var searchId = await _recommendationService.SubmitAsync(request.Vector, request.Language, cancellationToken);
        return Accepted(new RecommendationAcceptedResponse(searchId));
    }

    [HttpGet("{searchId:guid}")]
    [ProducesResponseType(typeof(RecommendationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendation(
        [FromRoute] Guid searchId,
        CancellationToken cancellationToken)
    {
        var result = await _recommendationService.GetAsync(searchId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new RecommendationStatusResponse(result.Status, result.Results));
    }
}
