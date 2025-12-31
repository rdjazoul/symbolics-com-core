using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class AirtableService : IAirtableService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AirtableOptions> _optionsMonitor;
    private readonly ILogger<AirtableService> _logger;

    public AirtableService(
        HttpClient httpClient,
        IOptionsMonitor<AirtableOptions> optionsMonitor,
        ILogger<AirtableService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task CreateRecommendationsAsync(
        IReadOnlyList<AirtableRecommendationRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records is null || records.Count == 0)
        {
            return;
        }

        var options = _optionsMonitor.CurrentValue;
        var requestUri = BuildRequestUri(options);
        var requestPayload = new AirtableCreateRecordsRequest(
            records.Select(record => new AirtableRecord(
                new Dictionary<string, object?>
                {
                    ["Name"] = record.Name,
                    ["Url Twitch"] = record.UrlTwitch,
                    ["Description"] = record.Description,
                    ["Persona"] = record.Persona,
                    ["Language"] = record.Language,
                    ["Email"] = record.Email,
                    ["Select"] = record.Select
                }))
            .ToList());

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestPayload, options: SerializerOptions)
        };

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            throw new InvalidOperationException("AirtableOptions.AccessToken is not configured.");
        }

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Airtable API error response: {ErrorContent}", errorContent);
        }

        response.EnsureSuccessStatusCode();
    }

    private static string BuildRequestUri(AirtableOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("AirtableOptions.BaseUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseId))
        {
            throw new InvalidOperationException("AirtableOptions.BaseId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.TableId))
        {
            throw new InvalidOperationException("AirtableOptions.TableId is not configured.");
        }

        var baseUrl = options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{options.BaseId}/{options.TableId}";
    }

    private sealed record AirtableCreateRecordsRequest(IReadOnlyList<AirtableRecord> Records);

    private sealed record AirtableRecord(IReadOnlyDictionary<string, object?> Fields);
}
