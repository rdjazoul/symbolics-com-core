using Microsoft.AspNetCore.Mvc;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Repositories;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/streamers")]
[ApiKey(ApiKeyScope.Service)]
public sealed class StreamersController(IStreamerRepository streamerRepository) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    private readonly IStreamerRepository _streamerRepository = streamerRepository;

    [HttpGet]
    [ProducesResponseType(typeof(StreamerListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStreamers(
        [FromQuery] string? language,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var resolvedPage = page < 1 ? DefaultPage : page;
        var resolvedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var result = await _streamerRepository.GetStreamersAsync(
            language,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var response = new StreamerListResponse(
            result.Items
                .Select(item => new StreamerResponse(
                    item.StreamerId,
                    item.TwitchId,
                    item.Language,
                    item.HasEmail,
                    item.TwitchLogin,
                    item.TwitchName,
                    $"https://www.twitch.tv/{item.TwitchLogin}"))
                .ToList(),
            result.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }
}
