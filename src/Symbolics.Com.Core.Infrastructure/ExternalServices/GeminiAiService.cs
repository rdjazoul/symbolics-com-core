using System.IO;
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
    private const string StreamerResearchPromptFile = "StreamerResearch.user.txt";
    private const string CampaignFusionPromptFileName = "CampaignFusion.txt";
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<GeminiOptions> _optionsMonitor;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly string _gameSystemPrompt;
    private readonly string _gameUserPromptTemplate;
    private readonly string _streamerSystemPrompt;
    private readonly string _streamerUserPromptTemplate;
    private readonly string _streamerResearchPromptTemplate;
    private readonly string _campaignFusionPromptPath;

    public GeminiAiService(
        HttpClient httpClient,
        IOptionsMonitor<GeminiOptions> optionsMonitor,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _gameSystemPrompt = LoadPrompt(GameSystemPromptFile);
        _gameUserPromptTemplate = LoadPrompt(GameUserPromptFile);
        _streamerSystemPrompt = LoadPrompt(StreamerSystemPromptFile);
        _streamerUserPromptTemplate = LoadPrompt(StreamerUserPromptFile);
        _streamerResearchPromptTemplate = LoadPrompt(StreamerResearchPromptFile);
        _campaignFusionPromptPath = Path.Combine(AppContext.BaseDirectory, "AiPrompts", CampaignFusionPromptFileName);
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

            var textResponse = await SendTextRequestAsync(options, requestBody, stopwatch);
            if (string.IsNullOrWhiteSpace(textResponse.Text))
            {
                throw new AiResponseFormatException("Gemini returned an empty vector description for the game.");
            }

            return new AiGameDescriptionResponse
            {
                VectorDescription = textResponse.Text,
                Consumption = BuildConsumptionMetrics(options.DescriptionModel, textResponse.Usage, stopwatch)
            };
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
        var researchStopwatch = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch? structuredStopwatch = null;
        try
        {
            var researchPrompt = _streamerResearchPromptTemplate
                .Replace("{twitchId}", twitchId, StringComparison.Ordinal)
                .Replace("{login}", login, StringComparison.Ordinal)
                .Replace("{displayName}", displayName, StringComparison.Ordinal)
                .Replace("{url}", url, StringComparison.Ordinal)
                .Replace("{rawDescription}", rawDescription, StringComparison.Ordinal);
            var researchRequest = BuildStreamerResearchRequest(_streamerSystemPrompt, researchPrompt);
            var researchResponse = await SendTextRequestAsync(options, researchRequest, researchStopwatch);
            if (string.IsNullOrWhiteSpace(researchResponse.Text))
            {
                throw new AiResponseFormatException("Gemini returned an empty streamer research payload.");
            }

            structuredStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var userPrompt = _streamerUserPromptTemplate
                .Replace("{twitchId}", twitchId, StringComparison.Ordinal)
                .Replace("{login}", login, StringComparison.Ordinal)
                .Replace("{displayName}", displayName, StringComparison.Ordinal)
                .Replace("{url}", url, StringComparison.Ordinal)
                .Replace("{rawDescription}", rawDescription, StringComparison.Ordinal)
                .Replace("{research}", researchResponse.Text, StringComparison.Ordinal);
            var requestBody = BuildStreamerRequest(_streamerSystemPrompt, userPrompt);

            _logger.LogInformation(
                "Generating streamer descriptions with Gemini. Login: {Login}, TwitchId: {TwitchId}",
                login,
                twitchId);

            var response = await SendRequestAsync<AiStreamerDescriptionsResponse>(options, requestBody, structuredStopwatch);
            response.Email = NormalizeEmail(response.Email);

            if (string.IsNullOrWhiteSpace(response.VectorDescription) || string.IsNullOrWhiteSpace(response.PersonaDescription))
            {
                throw new AiResponseFormatException("Gemini returned an empty streamer description payload.");
            }

            var researchConsumption = BuildConsumptionMetrics(options.DescriptionModel, researchResponse.Usage, researchStopwatch);
            response.Consumption = SumConsumption(researchConsumption, response.Consumption);

            return response;
        }
        catch (Exception ex) when (ex is not AiResponseFormatException)
        {
            researchStopwatch.Stop();
            structuredStopwatch?.Stop();
            _logger.LogError(ex, "Failed to generate streamer descriptions for {Login}.", login);
            throw;
        }
    }

    public async Task<string> MergeAndOptimizeDescriptions(string gameDescription, string campaignDescription)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var systemPrompt = await LoadFilePromptAsync(_campaignFusionPromptPath);
            var userPrompt = BuildCampaignFusionPrompt(gameDescription, campaignDescription);
            var requestBody = BuildTextRequest(systemPrompt, userPrompt);

            _logger.LogInformation("Merging and optimizing campaign descriptions with Gemini.");

            var response = await SendTextRequestAsync(options, requestBody, stopwatch);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new AiResponseFormatException("Gemini returned an empty optimized campaign description.");
            }

            return response.Text;
        }
        catch (Exception ex) when (ex is not AiResponseFormatException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to merge and optimize campaign descriptions.");
            throw;
        }
    }

    private async Task<TResponse> SendRequestAsync<TResponse>(
        GeminiOptions options,
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
        _logger.LogInformation("Request headers: {@Headers}", request.Headers.ToDictionary(h => h.Key, h => 
            string.Join(", ", h.Value.Select(v => 
                string.IsNullOrWhiteSpace(v) ? v : 
                v.Length > 4 ? $"...{v[^4..]}" : v))));
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

    private async Task<GeminiTextResponse> SendTextRequestAsync(
        GeminiOptions options,
        JsonObject requestBody,
        System.Diagnostics.Stopwatch stopwatch)
    {
        var requestUri = BuildRequestUri(options);

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody, typeof(JsonObject), new MediaTypeHeaderValue("application/json"), ResponseSerializerOptions)
        };
        request.Headers.Add("x-goog-api-key", options.DescriptionApiKey);

        _logger.LogInformation("Sending Gemini API request: {Method} {Uri}", request.Method, requestUri);
                _logger.LogInformation("Request headers: {@Headers}", request.Headers.ToDictionary(h => h.Key, h => 
            string.Join(", ", h.Value.Select(v => 
                string.IsNullOrWhiteSpace(v) ? v : 
                v.Length > 4 ? $"...{v[^4..]}" : v))));
        _logger.LogInformation("Request body: {RequestBody}", requestBody.ToString());

        var response = await _httpClient.SendAsync(request);

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
        var text = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiResponseFormatException("Gemini response did not contain any text payload.");
        }

        stopwatch.Stop();

        return new GeminiTextResponse(text, envelope?.UsageMetadata);
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
            CachedUnits = usage?.CachedContentTokenCount ?? 0,
            ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }

    private static ConsumptionMetrics SumConsumption(ConsumptionMetrics first, ConsumptionMetrics second)
    {
        return new ConsumptionMetrics
        {
            Model = string.IsNullOrWhiteSpace(second.Model) ? first.Model : second.Model,
            InputUnits = first.InputUnits + second.InputUnits,
            OutputUnits = first.OutputUnits + second.OutputUnits,
            CachedUnits = first.CachedUnits + second.CachedUnits,
            ProcessingTimeMs = first.ProcessingTimeMs + second.ProcessingTimeMs
        };
    }

    private static string BuildRequestUri(GeminiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DescriptionBaseUrl))
        {
            throw new InvalidOperationException("GeminiOptions.DescriptionBaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.DescriptionModel))
        {
            throw new InvalidOperationException("GeminiOptions.DescriptionModel is not configured.");
        }

        var baseUrl = options.DescriptionBaseUrl.TrimEnd('/');
        return $"{baseUrl}/models/{options.DescriptionModel}:generateContent";
    }

    private static JsonObject BuildGameRequest(string systemPrompt, string userPrompt)
    {
        return BuildTextRequest(systemPrompt, userPrompt, includeGoogleSearch: true, includeUrlContext: true);
    }

    private static JsonObject BuildStreamerRequest(string systemPrompt, string userPrompt)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["vector_description"] = new JsonObject
                {
                    ["type"] = "string"
                },
                ["persona_description"] = new JsonObject
                {
                    ["type"] = "string"
                },
                ["email"] = new JsonObject
                {
                    ["type"] = new JsonArray("string", "null")
                },
                ["language"] = new JsonObject
                {
                    ["type"] = new JsonArray("string", "null")
                }
            },
            ["required"] = new JsonArray("vector_description", "persona_description", "language")
        };

        return BuildStructuredRequest(systemPrompt, userPrompt, schema);
    }

    private static JsonObject BuildStreamerResearchRequest(string systemPrompt, string userPrompt)
    {
        return BuildTextRequest(systemPrompt, userPrompt, includeGoogleSearch: false, includeUrlContext: true);
    }

    private static JsonObject BuildStructuredRequest(string systemPrompt, string userPrompt, JsonObject schema)
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
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseJsonSchema"] = schema
            }
        };
    }

    private static JsonObject BuildTextRequest(string systemPrompt, string userPrompt, bool includeGoogleSearch = false, bool includeUrlContext = false)
    {
        var request = new JsonObject
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
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "text/plain"
            }
        };

        var tools = new JsonArray();
        if (includeGoogleSearch)
        {
            tools.Add(new JsonObject { ["googleSearch"] = new JsonObject() });
        }
        if (includeUrlContext)
        {
            tools.Add(new JsonObject { ["urlContext"] = new JsonObject() });
        }
        
        if (tools.Count > 0)
        {
            request["tools"] = tools;
        }

        return request;
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

    private static string BuildCampaignFusionPrompt(string gameDescription, string campaignDescription)
    {
        return $"Game description:\n{gameDescription}\n\nCampaign description:\n{campaignDescription}";
    }

    private static async Task<string> LoadFilePromptAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Prompt file '{filePath}' not found.");
        }

        var contents = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return contents.Trim();
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

    private sealed record GeminiTextResponse(string Text, GeminiUsageMetadata? Usage);

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
        public long CachedContentTokenCount { get; set; }
    }
}
