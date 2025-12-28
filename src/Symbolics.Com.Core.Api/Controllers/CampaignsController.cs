using Microsoft.AspNetCore.Mvc;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Infrastructure.ExternalServices;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[ApiKey(ApiKeyScope.Service)]
public sealed class CampaignsController(
    ICampaignVectorizationService campaignVectorizationService,
    ILogger<CampaignsController> logger) : ControllerBase
{
    private readonly ICampaignVectorizationService _campaignVectorizationService = campaignVectorizationService;
    private readonly ILogger<CampaignsController> _logger = logger;

    [HttpPost("vectorize")]
    [ProducesResponseType(typeof(VectorizeCampaignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> VectorizeCampaign(
        [FromBody] VectorizeCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GameDescription) || string.IsNullOrWhiteSpace(request.CampaignDescription))
        {
            return BadRequest("GameDescription and CampaignDescription are required.");
        }

        try
        {
            var result = await _campaignVectorizationService.VectorizeAsync(
                request.GameDescription,
                request.CampaignDescription,
                cancellationToken);

            return Ok(new VectorizeCampaignResponse(result.Vector, result.OptimizedDescription));
        }
        catch (Exception ex) when (ex is HttpRequestException or AiResponseFormatException)
        {
            _logger.LogError(ex, "Failed to vectorize campaign descriptions via Gemini.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "AI service unavailable.");
        }
    }
}
