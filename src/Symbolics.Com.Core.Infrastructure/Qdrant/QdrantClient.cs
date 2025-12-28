using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.Qdrant;

public sealed class QdrantClient : IQdrantClient
{
    private const int VectorSize = 1536;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<QdrantOptions> _optionsMonitor;
    private readonly ILogger<QdrantClient> _logger;

    public QdrantClient(
        HttpClient httpClient,
        IOptionsMonitor<QdrantOptions> optionsMonitor,
        ILogger<QdrantClient> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public bool SaveGameDescription(Guid gameId, float[] vector, string vectorDescription)
    {
        return SaveDescription(
            "GameVectors",
            "game_id",
            gameId,
            vector,
            vectorDescription);
    }

    public bool SaveStreamerDescription(Guid streamerId, float[] vector, string vectorDescription, string language)
    {
        return SaveDescription(
            "StreamerVectors",
            "streamer_id",
            streamerId,
            vector,
            vectorDescription,
            language);
    }

    public async Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.UrlHttp))
        {
            _logger.LogWarning("Qdrant URL is missing from configuration.");
            return false;
        }

        var healthUrl = $"{options.UrlHttp.TrimEnd('/')}/healthz";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, healthUrl);
            AddApiKeyHeader(request, options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reach Qdrant health endpoint at {HealthUrl}.", healthUrl);
            return false;
        }
    }

    public async Task EnsureCollectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureCollectionAsync("GameVectors", cancellationToken);
            await EnsureCollectionAsync("StreamerVectors", cancellationToken);
            await EnsureCollectionAsync("CampaignVectors", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while ensuring Qdrant collections.");
            throw;
        }
    }

    private async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.UrlHttp))
        {
            throw new InvalidOperationException("Qdrant URL is missing from configuration.");
        }

        var collectionUrl = $"{options.UrlHttp.TrimEnd('/')}/collections/{collectionName}";
        _logger.LogInformation("Ensuring Qdrant collection {CollectionName} at {CollectionUrl}.", collectionName, collectionUrl);

        try
        {
            using var existsRequest = new HttpRequestMessage(HttpMethod.Get, collectionUrl);
            AddApiKeyHeader(existsRequest, options.ApiKey);

            using var existsResponse = await _httpClient.SendAsync(existsRequest, cancellationToken);
            if (existsResponse.StatusCode == HttpStatusCode.NotFound)
            {
                await CreateCollectionAsync(collectionUrl, options.ApiKey, cancellationToken);
                _logger.LogInformation("Created Qdrant collection {CollectionName}.", collectionName);
                return;
            }

            existsResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure Qdrant collection {CollectionName}.", collectionName);
            throw;
        }
    }

    private async Task CreateCollectionAsync(string collectionUrl, string apiKey, CancellationToken cancellationToken)
    {
        var payload = new
        {
            vectors = new
            {
                size = VectorSize,
                distance = "Cosine"
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var createRequest = new HttpRequestMessage(HttpMethod.Put, collectionUrl)
        {
            Content = content
        };
        AddApiKeyHeader(createRequest, apiKey);

        using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken);
        createResponse.EnsureSuccessStatusCode();
    }

    private static void AddApiKeyHeader(HttpRequestMessage request, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }
    }

    private bool SaveDescription(
        string collectionName,
        string payloadKey,
        Guid entityId,
        float[] vector,
        string vectorDescription,
        string? language = null)
    {
        return SaveDescriptionAsync(collectionName, payloadKey, entityId, vector, vectorDescription, language)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<bool> SaveDescriptionAsync(
        string collectionName,
        string payloadKey,
        Guid entityId,
        float[] vector,
        string vectorDescription,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (vector is null || vector.Length == 0)
        {
            throw new ArgumentException("Vector cannot be empty.", nameof(vector));
        }

        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.UrlHttp))
        {
            throw new InvalidOperationException("Qdrant URL is missing from configuration.");
        }

        var pointId = entityId.ToString();
        var url = $"{options.UrlHttp.TrimEnd('/')}/collections/{collectionName}/points?wait=true";
        
        var payloadDict = new Dictionary<string, string>
        {
            [payloadKey] = pointId,
            ["vector_description"] = vectorDescription
        };
        
        if (!string.IsNullOrWhiteSpace(language))
        {
            payloadDict["language"] = language;
        }
        
        var payload = new
        {
            points = new[]
            {
                new
                {
                    id = pointId,
                    vector,
                    payload = payloadDict
                }
            }
        };

        try
        {
            using var response = await SendWithRetryAsync(
                url,
                options.ApiKey,
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Qdrant upsert failed for {CollectionName} with status {StatusCode}: {ErrorContent}",
                    collectionName,
                    response.StatusCode,
                    errorContent);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Qdrant vector for {CollectionName}.", collectionName);
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string url,
        string apiKey,
        object payload,
        CancellationToken cancellationToken)
    {
        var retryPolicy = BuildRetryPolicy();

        return await retryPolicy.ExecuteAsync(async () =>
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload, SerializerOptions),
                Encoding.UTF8,
                "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = content
            };
            AddApiKeyHeader(request, apiKey);

            return await _httpClient.SendAsync(request, cancellationToken);
        });
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
                        _logger.LogWarning(outcome.Exception, "Retrying Qdrant request (attempt {RetryCount}).", retryCount);
                        return;
                    }

                    _logger.LogWarning(
                        "Retrying Qdrant request due to {StatusCode} (attempt {RetryCount}).",
                        outcome.Result?.StatusCode,
                        retryCount);
                });
    }
}
