using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class TwitchTokenHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TwitchOptions> _optionsMonitor;
    private readonly ILogger<TwitchTokenHandler> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private TwitchToken? _token;

    public TwitchTokenHandler(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TwitchOptions> optionsMonitor,
        ILogger<TwitchTokenHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var token = await GetTokenAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Remove("Client-Id");
        request.Headers.Add("Client-Id", options.ClientId);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<TwitchToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && !_token.IsExpired)
        {
            return _token;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && !_token.IsExpired)
            {
                return _token;
            }

            _token = await RequestTokenAsync(cancellationToken);
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<TwitchToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["grant_type"] = "client_credentials"
        };

        var httpClient = _httpClientFactory.CreateClient("TwitchAuth");
        using var response = await httpClient.PostAsync(
            "https://id.twitch.tv/oauth2/token",
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to retrieve Twitch app access token. Status: {StatusCode}. Response: {ResponseBody}",
                response.StatusCode,
                responseBody ?? "<empty>");
            response.EnsureSuccessStatusCode();
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Twitch token response was empty.");

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
        return new TwitchToken(tokenResponse.AccessToken, expiresAt);
    }

    private sealed record TwitchToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    }

    private sealed class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}
