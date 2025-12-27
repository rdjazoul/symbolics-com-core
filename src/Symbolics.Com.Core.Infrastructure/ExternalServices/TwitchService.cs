using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class TwitchService : ITwitchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TwitchOptions> _optionsMonitor;
    private readonly ILogger<TwitchService> _logger;

    public TwitchService(HttpClient httpClient, IOptionsMonitor<TwitchOptions> optionsMonitor, ILogger<TwitchService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<TwitchStreamResponse> GetStreams(string? cursor)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            var requestUri = BuildStreamsUri(cursor);
            _logger.LogInformation(
                "Fetching Twitch streams. BaseUrl: {BaseUrl}, Cursor: {Cursor}",
                options.BaseUrl,
                cursor ?? "<none>");

            using var response = await _httpClient.GetAsync(requestUri);
            await EnsureSuccessAsync(response, "streams", cursor);

            LogRateLimit(response, "streams");

            var payload = await response.Content.ReadFromJsonAsync<TwitchStreamEnvelope>(JsonOptions);
            if (payload is null || payload.Data is null)
            {
                return new TwitchStreamResponse
                {
                    Cursor = payload?.Pagination?.Cursor
                };
            }

            var mappedStreams = payload.Data.Select(MapStream).ToList();

            return new TwitchStreamResponse
            {
                Cursor = payload.Pagination?.Cursor,
                Data = mappedStreams
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch streams for cursor {Cursor}.", cursor ?? "<none>");
            throw;
        }
    }

    public async Task<TwitchStreamerResponse> GetStreamerInfos(string twitchLogin)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            _logger.LogInformation(
                "Fetching Twitch streamer info. BaseUrl: {BaseUrl}, Login: {Login}",
                options.BaseUrl,
                twitchLogin);

            var requestUri = $"users?login={Uri.EscapeDataString(twitchLogin)}";
            using var response = await _httpClient.GetAsync(requestUri);
            await EnsureSuccessAsync(response, "users", twitchLogin);

            LogRateLimit(response, "users");

            var payload = await response.Content.ReadFromJsonAsync<TwitchUserEnvelope>(JsonOptions);
            var user = payload?.Data?.FirstOrDefault();
            if (user is null)
            {
                throw new HttpRequestException($"Twitch user not found for login '{twitchLogin}'.", null, HttpStatusCode.NotFound);
            }

            return new TwitchStreamerResponse
            {
                Id = user.Id,
                Login = user.Login,
                DisplayName = user.DisplayName,
                Type = user.Type,
                BroadcasterType = user.BroadcasterType,
                Description = user.Description,
                ProfileImageUrl = user.ProfileImageUrl,
                OfflineImageUrl = user.OfflineImageUrl,
                ViewCount = user.ViewCount,
                CreatedAt = user.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch streamer info for {Login}.", twitchLogin);
            throw;
        }
    }

    public async Task<TwitchGameResponse> GetGameInfos(string twitchGameId)
    {
        var options = _optionsMonitor.CurrentValue;
        try
        {
            _logger.LogInformation(
                "Fetching Twitch game info. BaseUrl: {BaseUrl}, GameId: {GameId}",
                options.BaseUrl,
                twitchGameId);

            var requestUri = $"games?id={Uri.EscapeDataString(twitchGameId)}";
            using var response = await _httpClient.GetAsync(requestUri);
            await EnsureSuccessAsync(response, "games", twitchGameId);

            LogRateLimit(response, "games");

            var payload = await response.Content.ReadFromJsonAsync<TwitchGameEnvelope>(JsonOptions);
            var game = payload?.Data?.FirstOrDefault();
            if (game is null)
            {
                throw new HttpRequestException($"Twitch game not found for id '{twitchGameId}'.", null, HttpStatusCode.NotFound);
            }

            return new TwitchGameResponse
            {
                Id = game.Id,
                Name = game.Name,
                BoxArtUrl = game.BoxArtUrl,
                IgdbId = game.IgdbId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitch game info for {GameId}.", twitchGameId);
            throw;
        }
    }

    private static string BuildStreamsUri(string? cursor)
    {
        var parameters = new List<string> { "first=100" };
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            parameters.Add($"after={Uri.EscapeDataString(cursor)}");
        }

        return $"streams?{string.Join("&", parameters)}";
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string endpointName, string? identifier)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = response.StatusCode;
        var responseBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        _logger.LogWarning(
            "Twitch {Endpoint} request failed with status {StatusCode} for {Identifier}. Response: {ResponseBody}",
            endpointName,
            statusCode,
            identifier ?? "<none>",
            responseBody ?? "<empty>");

        response.EnsureSuccessStatusCode();
    }

    private void LogRateLimit(HttpResponseMessage response, string endpointName)
    {
        if (!response.Headers.TryGetValues("Ratelimit-Remaining", out var remainingValues))
        {
            return;
        }

        var remainingValue = remainingValues.FirstOrDefault();
        if (!int.TryParse(remainingValue, out var remaining))
        {
            return;
        }

        response.Headers.TryGetValues("Ratelimit-Reset", out var resetValues);
        var resetValue = resetValues?.FirstOrDefault();

        if (remaining <= 10)
        {
            _logger.LogWarning(
                "Twitch {Endpoint} rate limit nearing exhaustion. Remaining: {Remaining}, Reset: {Reset}.",
                endpointName,
                remaining,
                resetValue ?? "<unknown>");
        }
        else
        {
            _logger.LogDebug(
                "Twitch {Endpoint} rate limit remaining: {Remaining}, Reset: {Reset}.",
                endpointName,
                remaining,
                resetValue ?? "<unknown>");
        }
    }

    private static TwitchStreamResponse MapStream(TwitchStreamDto stream)
    {
        return new TwitchStreamResponse
        {
            Id = stream.Id,
            UserId = stream.UserId,
            UserLogin = stream.UserLogin,
            UserName = stream.UserName,
            GameId = stream.GameId,
            GameName = stream.GameName,
            Type = stream.Type,
            Title = stream.Title,
            ViewerCount = stream.ViewerCount,
            StartedAt = stream.StartedAt,
            Language = stream.Language,
            ThumbnailUrl = stream.ThumbnailUrl,
            TagIds = stream.TagIds ?? Array.Empty<string>(),
            Tags = stream.Tags ?? Array.Empty<string>(),
            IsMature = stream.IsMature
        };
    }

    private sealed class TwitchStreamEnvelope
    {
        [JsonPropertyName("data")]
        public List<TwitchStreamDto> Data { get; set; } = new();

        [JsonPropertyName("pagination")]
        public TwitchPagination? Pagination { get; set; }
    }

    private sealed class TwitchPagination
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }
    }

    private sealed class TwitchStreamDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; } = string.Empty;

        [JsonPropertyName("user_name")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("game_id")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("game_name")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; } = string.Empty;

        [JsonPropertyName("tag_ids")]
        public string[]? TagIds { get; set; }

        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }

        [JsonPropertyName("is_mature")]
        public bool IsMature { get; set; }
    }

    private sealed class TwitchUserEnvelope
    {
        [JsonPropertyName("data")]
        public List<TwitchUserDto> Data { get; set; } = new();
    }

    private sealed class TwitchUserDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("broadcaster_type")]
        public string BroadcasterType { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("offline_image_url")]
        public string OfflineImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("view_count")]
        public int ViewCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    private sealed class TwitchGameEnvelope
    {
        [JsonPropertyName("data")]
        public List<TwitchGameDto> Data { get; set; } = new();
    }

    private sealed class TwitchGameDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("box_art_url")]
        public string BoxArtUrl { get; set; } = string.Empty;

        [JsonPropertyName("igdb_id")]
        public string IgdbId { get; set; } = string.Empty;
    }
}
