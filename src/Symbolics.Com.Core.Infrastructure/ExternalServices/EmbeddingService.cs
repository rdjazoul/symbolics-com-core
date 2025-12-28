using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class EmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions ResponseSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<GeminiOptions> _optionsMonitor;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        HttpClient httpClient,
        IOptionsMonitor<GeminiOptions> optionsMonitor,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<EmbeddingResponse> GenerateEmbedding(string text)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding text cannot be empty.", nameof(text));
        }

        var requestUri = BuildRequestUri(options);
        var requestPayload = BuildRequestPayload(text, options);

        var retryPolicy = BuildRetryPolicy();
        using var response = await retryPolicy.ExecuteAsync(async () =>
        {
            using var content = new StringContent(
                requestPayload.ToJsonString(),
                Encoding.UTF8,
                "application/json");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            };
            httpRequest.Headers.Add("x-goog-api-key", options.EmbeddingApiKey);
            return await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini embedding API error response: {ErrorContent}", errorContent);
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(payload, ResponseSerializerOptions);
        var vector = envelope?.Embedding?.Values ?? [];
        if (vector.Length == 0)
        {
            throw new InvalidOperationException("Gemini embedding response did not contain any vector values.");
        }

        var dimension = options.EmbeddingDimensions > 0 ? options.EmbeddingDimensions : 1536;
        if (vector.Length != dimension)
        {
            throw new InvalidOperationException($"Gemini embedding response returned {vector.Length} dimensions (expected {dimension}).");
        }

        var promptTokens = envelope?.UsageMetadata?.PromptTokenCount ?? 0;
        _logger.LogInformation("Gemini embedding prompt tokens: {PromptTokenCount}", promptTokens);

        stopwatch.Stop();

        return new EmbeddingResponse
        {
            Vector = vector,
            Consumption = new ConsumptionMetrics
            {
                Model = options.EmbeddingModel,
                InputUnits = promptTokens,
                OutputUnits = vector.Length,
                CachedUnits = 0,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            }
        };
    }

    private AsyncRetryPolicy<HttpResponseMessage> BuildRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, _) =>
                {
                    outcome.Result?.Dispose();
                    if (outcome.Exception is not null)
                    {
                        _logger.LogWarning(outcome.Exception, "Retrying Gemini embedding request (attempt {RetryCount}).", retryCount);
                        return;
                    }

                    _logger.LogWarning(
                        "Retrying Gemini embedding request due to {StatusCode} (attempt {RetryCount}).",
                        outcome.Result?.StatusCode,
                        retryCount);
                });
    }

    private static JsonObject BuildRequestPayload(string text, GeminiOptions options)
    {
        var modelName = NormalizeModel(options.EmbeddingModel);
        var dimension = options.EmbeddingDimensions > 0 ? options.EmbeddingDimensions : 1536;

        return new JsonObject
        {
            ["model"] = modelName,
            ["task_type"] = "SEMANTIC_SIMILARITY",
            ["content"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = text
                    }
                }
            },
            ["output_dimensionality"] = dimension
        };
    }

    private static string BuildRequestUri(GeminiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingBaseUrl))
        {
            throw new InvalidOperationException("GeminiOptions.EmbeddingBaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            throw new InvalidOperationException("GeminiOptions.EmbeddingModel is not configured.");
        }

        var baseUrl = options.EmbeddingBaseUrl.TrimEnd('/');
        var modelName = NormalizeModel(options.EmbeddingModel);
        return $"{baseUrl}/{modelName}:embedContent";
    }

    private static string NormalizeModel(string model)
    {
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model
            : $"models/{model}";
    }

    private sealed class GeminiEmbeddingResponse
    {
        public GeminiEmbedding? Embedding { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private sealed class GeminiEmbedding
    {
        public float[] Values { get; set; } = [];
    }

    private sealed class GeminiUsageMetadata
    {
        public long PromptTokenCount { get; set; }
    }
}
