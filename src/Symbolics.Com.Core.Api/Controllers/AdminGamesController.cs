using Microsoft.AspNetCore.Mvc;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/admin/games")]
[ApiKey(ApiKeyScope.Admin)]
public sealed class AdminGamesController(IAdminGameService adminGameService) : ControllerBase
{
    private readonly IAdminGameService _adminGameService = adminGameService;

    [HttpGet("missing-igdb")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminGameMissingIgdbResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGamesMissingIgdb(CancellationToken cancellationToken)
    {
        var games = await _adminGameService.GetGamesMissingIgdbAsync(cancellationToken);
        var response = games
            .Select(game => new AdminGameMissingIgdbResponse(
                game.GameId,
                game.TwitchId,
                game.Name,
                game.VectorDescription))
            .ToList();

        return Ok(response);
    }

    [HttpPatch("{gameId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGame(
        Guid gameId,
        [FromBody] AdminGameUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!IsValidIgdbId(request.IgdbId))
        {
            return BadRequest("IgdbId must be a positive numeric string.");
        }

        var result = await _adminGameService.UpdateGameAsync(
            gameId,
            request.IgdbId,
            request.ManualDescription,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static bool IsValidIgdbId(string? igdbId)
    {
        if (igdbId is null)
        {
            return true;
        }

        var trimmed = igdbId.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        return long.TryParse(trimmed, out var parsed) && parsed >= -1;
    }
}
