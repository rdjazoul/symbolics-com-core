using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class GeminiAiService : IAiService
{
    private const string GameSystemPromptFile = "GameDescription.system.txt";
    private const string GameUserPromptFile = "GameDescription.user.txt";
    private const string StreamerSystemPromptFile = "StreamerDescription.system.txt";
    private const string StreamerUserPromptFile = "StreamerDescription.user.txt";
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly string _gameSystemPrompt;
    private readonly string _gameUserPromptTemplate;
    private readonly string _streamerSystemPrompt;
    private readonly string _streamerUserPromptTemplate;

    public GeminiAiService(
        HttpClient httpClient,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _gameSystemPrompt = LoadPrompt(GameSystemPromptFile);
        _gameUserPromptTemplate = LoadPrompt(GameUserPromptFile);
        _streamerSystemPrompt = LoadPrompt(StreamerSystemPromptFile);
        _streamerUserPromptTemplate = LoadPrompt(StreamerUserPromptFile);
    }

    public async Task<AiGameDescriptionResponse> GenerateGameDescription(string twitchGameId, string gameName)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var userPrompt = _gameUserPromptTemplate
                .Replace("{gameName}", gameName, StringComparison.Ordinal)
                .Replace("{twitchGameId}", twitchGameId, StringComparison.Ordinal);
            var requestBody = BuildGameRequest(_gameSystemPrompt, userPrompt);

            _logger.LogInformation(
                "Generating game description with Gemini. Game: {GameName}, TwitchId: {TwitchGameId}",
                gameName,
                twitchGameId);

            var response = await SendRequestAsync<AiGameDescriptionResponse>(options, requestBody, stopwatch);
            if (string.IsNullOrWhiteSpace(response.VectorDescription))
            {
                throw new AiResponseFormatException("Gemini returned an empty vector description for the game.");
            }

            return response;
        }
        catch (Exception ex) when (ex is not AiResponseFormatException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to generate game description for {GameName}.", gameName);
            throw;
        }
    }

    public async Task<AiStreamerDescriptionsResponse> GenerateStreamerDescription(
        string twitchId,
        string login,
        string displayName,
        string url,
        string rawDescription)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var userPrompt = _streamerUserPromptTemplate
                .Replace("{twitchId}", twitchId, StringComparison.Ordinal)
                .Replace("{login}", login, StringComparison.Ordinal)
                .Replace("{displayName}", displayName, StringComparison.Ordinal)
                .Replace("{url}", url, StringComparison.Ordinal)
                .Replace("{rawDescription}", rawDescription, StringComparison.Ordinal);
            var requestBody = BuildStreamerRequest(_streamerSystemPrompt, userPrompt);

            _logger.LogInformation(
                "Generating streamer descriptions with Gemini. Login: {Login}, TwitchId: {TwitchId}",
                login,
                twitchId);

            var response = await SendRequestAsync<AiStreamerDescriptionsResponse>(options, requestBody, stopwatch);
            response.Email = NormalizeEmail(response.Email);

            if (string.IsNullOrWhiteSpace(response.VectorDescription) || string.IsNullOrWhiteSpace(response.PersonaDescription))
            {
                throw new AiResponseFormatException("Gemini returned an empty streamer description payload.");
            }

            return response;
        }
        catch (Exception ex) when (ex is not AiResponseFormatException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to generate streamer descriptions for {Login}.", login);
            throw;
        }
    }

    private async Task<TResponse> SendRequestAsync<TResponse>(
        AiOptions options,
        JsonObject requestBody,
        System.Diagnostics.Stopwatch stopwatch)
        where TResponse : class
    {
        var requestUri = BuildRequestUri(options);
        
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody, typeof(JsonObject), new MediaTypeHeaderValue("application/json"), ResponseSerializerOptions)
        };
        request.Headers.Add("x-goog-api-key", options.DescriptionApiKey);
        
        // Log request details
        _logger.LogInformation("Sending Gemini API request: {Method} {Uri}", request.Method, requestUri);
        _logger.LogInformation("Request headers: {@Headers}", request.Headers.ToDictionary(h => h.Key, h => h.Value));
        _logger.LogInformation("Request body: {RequestBody}", requestBody.ToString());
        
        var response = await _httpClient.SendAsync(request);
        
        // Log response details
        _logger.LogInformation("Received Gemini API response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
        _logger.LogInformation("Response headers: {@Headers}", response.Headers.ToDictionary(h => h.Key, h => h.Value));
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API error response: {ErrorContent}", errorContent);
        }
        
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<GeminiResponse>(payload, ResponseSerializerOptions);
        var jsonContent = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            throw new AiResponseFormatException("Gemini response did not contain any JSON payload.");
        }

        TResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TResponse>(jsonContent, ResponseSerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new AiResponseFormatException("Gemini returned invalid JSON content.", ex);
        }

        if (parsed is null)
        {
            throw new AiResponseFormatException("Gemini returned an empty JSON payload.");
        }

        var usage = envelope?.UsageMetadata;
        stopwatch.Stop();

        switch (parsed)
        {
            case AiGameDescriptionResponse gameResponse:
                gameResponse.Consumption = BuildConsumptionMetrics(options.DescriptionModel, usage, stopwatch);
                break;
            case AiStreamerDescriptionsResponse streamerResponse:
                streamerResponse.Consumption = BuildConsumptionMetrics(options.DescriptionModel, usage, stopwatch);
                break;
        }

        return parsed;
    }

    private static ConsumptionMetrics BuildConsumptionMetrics(
        string model,
        GeminiUsageMetadata? usage,
        System.Diagnostics.Stopwatch stopwatch)
    {
        return new ConsumptionMetrics
        {
            Model = model,
            InputUnits = usage?.PromptTokenCount ?? 0,
            OutputUnits = usage?.CandidatesTokenCount ?? 0,
            ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private static string BuildRequestUri(AiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DescriptionBaseUrl))
        {
            throw new InvalidOperationException("AiOptions.DescriptionBaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.DescriptionModel))
        {
            throw new InvalidOperationException("AiOptions.DescriptionModel is not configured.");
        }

        var baseUrl = options.DescriptionBaseUrl.TrimEnd('/');
        return $"{baseUrl}/models/{options.DescriptionModel}:generateContent";
    }

    private static JsonObject BuildGameRequest(string systemPrompt, string userPrompt)
    {
        var schema = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["vector_description"] = new JsonObject
                {
                    ["type"] = "STRING"
                }
            },
            ["required"] = new JsonArray("vector_description")
        };

        return BuildRequest(systemPrompt, userPrompt, schema);
    }

    private static JsonObject BuildStreamerRequest(string systemPrompt, string userPrompt)
    {
        var schema = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["vector_description"] = new JsonObject
                {
                    ["type"] = "STRING"
                },
                ["persona_description"] = new JsonObject
                {
                    ["type"] = "STRING"
                },
                ["email"] = new JsonObject
                {
                    ["type"] = new JsonArray("STRING", "NULL")
                }
            },
            ["required"] = new JsonArray("vector_description", "persona_description")
        };

        return BuildRequest(systemPrompt, userPrompt, schema);
    }

    private static JsonObject BuildRequest(string systemPrompt, string userPrompt, JsonObject schema)
    {
        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt })
            },
            ["contents"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = userPrompt })
                }),
            ["tools"] = new JsonArray(new JsonObject { ["google_search_retrieval"] = new JsonObject() }),
            ["generationConfig"] = new JsonObject
            {
                ["response_mime_type"] = "application/json",
                ["response_schema"] = schema
            }
        };
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim();
        normalized = normalized.Replace("[at]", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("(at)", "@", StringComparison.OrdinalIgnoreCase)
            .Replace(" at ", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("[dot]", ".", StringComparison.OrdinalIgnoreCase)
            .Replace("(dot)", ".", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot ", ".", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Value : null;
    }

    private static string LoadPrompt(string fileName)
    {
        var assembly = typeof(GeminiAiService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new FileNotFoundException($"Prompt file '{fileName}' not found as an embedded resource.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new FileNotFoundException($"Prompt file '{fileName}' could not be loaded.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd().Trim();
    }

    private sealed class GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        public string? Text { get; set; }
    }

    private sealed class GeminiUsageMetadata
    {
        public long PromptTokenCount { get; set; }
        public long CandidatesTokenCount { get; set; }
    }
}
