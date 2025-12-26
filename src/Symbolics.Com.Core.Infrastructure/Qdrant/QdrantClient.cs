using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.Qdrant;

public sealed class QdrantClient : IQdrantClient
{
    private const int VectorSize = 1536;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<QdrantOptions> _optionsMonitor;

    public QdrantClient(HttpClient httpClient, IOptionsMonitor<QdrantOptions> optionsMonitor)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
    }

    public bool SaveGameDescription(Guid gameId, float[] vector)
    {
        return true;
    }

    public bool SaveStreamerDescription(Guid streamerId, float[] vector)
    {
        return true;
    }

    public async Task EnsureCollectionsAsync(CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync("GameVectors", cancellationToken);
        await EnsureCollectionAsync("StreamerVectors", cancellationToken);
        await EnsureCollectionAsync("CampaignVectors", cancellationToken);
    }

    private async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.UrlHttp))
        {
            throw new InvalidOperationException("Qdrant URL is missing from configuration.");
        }

        var collectionUrl = $"{options.UrlHttp.TrimEnd('/')}/collections/{collectionName}";
        using var existsRequest = new HttpRequestMessage(HttpMethod.Get, collectionUrl);
        AddApiKeyHeader(existsRequest, options.ApiKey);

        using var existsResponse = await _httpClient.SendAsync(existsRequest, cancellationToken);
        if (existsResponse.StatusCode == HttpStatusCode.NotFound)
        {
            await CreateCollectionAsync(collectionUrl, options.ApiKey, cancellationToken);
            return;
        }

        existsResponse.EnsureSuccessStatusCode();
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
}
