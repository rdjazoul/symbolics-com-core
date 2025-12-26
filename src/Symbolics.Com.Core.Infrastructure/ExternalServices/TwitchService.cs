using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class TwitchService : ITwitchService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TwitchOptions> _optionsMonitor;
    private readonly ILogger<TwitchService> _logger;

    public TwitchService(HttpClient httpClient, IOptionsMonitor<TwitchOptions> optionsMonitor, ILogger<TwitchService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public Task<TwitchStreamResponse> GetStreams(string? cursor)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            _logger.LogInformation(
                "Fetching Twitch streams (placeholder). BaseUrl: {BaseUrl}, Cursor: {Cursor}",
                options.BaseUrl,
                cursor ?? "<none>");

            var response = new TwitchStreamResponse
            {
                Cursor = "placeholder-cursor",
                Data =
                {
                    new TwitchStreamResponse
                    {
                        Id = "stream-placeholder",
                        UserId = "user-placeholder",
                        UserLogin = "placeholder_login",
                        UserName = "Placeholder",
                        GameId = "game-placeholder",
                        GameName = "Placeholder Game",
                        Type = "live",
                        Title = "Placeholder stream",
                        ViewerCount = 0,
                        StartedAt = DateTime.UtcNow,
                        Language = "fr",
                        ThumbnailUrl = "https://placehold.co/320x180",
                        TagIds = Array.Empty<string>(),
                        Tags = Array.Empty<string>(),
                        IsMature = false
                    }
                }
            };

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch streams for cursor {Cursor}.", cursor ?? "<none>");
            throw;
        }
    }

    public Task<TwitchStreamerResponse> GetStreamerInfos(string twitchLogin)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            _logger.LogInformation(
                "Fetching Twitch streamer info (placeholder). BaseUrl: {BaseUrl}, Login: {Login}",
                options.BaseUrl,
                twitchLogin);

            var response = new TwitchStreamerResponse
            {
                Id = "streamer-placeholder",
                Login = twitchLogin,
                DisplayName = "Placeholder Streamer",
                Type = "",
                BroadcasterType = "",
                Description = "Placeholder Twitch streamer description.",
                ProfileImageUrl = "https://placehold.co/96x96",
                OfflineImageUrl = "https://placehold.co/1920x1080",
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch streamer info for {Login}.", twitchLogin);
            throw;
        }
    }

    public Task<TwitchGameResponse> GetGameInfos(string twitchGameId)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            _logger.LogInformation(
                "Fetching Twitch game info (placeholder). BaseUrl: {BaseUrl}, GameId: {GameId}",
                options.BaseUrl,
                twitchGameId);

            var response = new TwitchGameResponse
            {
                Id = twitchGameId,
                Name = "Placeholder Game",
                BoxArtUrl = "https://placehold.co/264x352",
                IgdbId = "placeholder-igdb"
            };

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch game info for {GameId}.", twitchGameId);
            throw;
        }
    }
}
