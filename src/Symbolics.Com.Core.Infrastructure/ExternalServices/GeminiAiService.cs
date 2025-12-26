using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(HttpClient httpClient, IOptionsMonitor<AiOptions> optionsMonitor, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public Task<string> GenerateGameDescription(string gameName)
    {
        var options = _optionsMonitor.CurrentValue;
        _logger.LogInformation(
            "Generating game description (placeholder). BaseUrl: {BaseUrl}, Game: {GameName}",
            options.DescriptionBaseUrl,
            gameName);

        return Task.FromResult($"Placeholder description for game '{gameName}'.");
    }

    public Task<AiStreamerDescriptionsResponse> GenerateStreamerDescription(string bio, string login)
    {
        var options = _optionsMonitor.CurrentValue;
        _logger.LogInformation(
            "Generating streamer descriptions (placeholder). BaseUrl: {BaseUrl}, Login: {Login}",
            options.DescriptionBaseUrl,
            login);

        var response = new AiStreamerDescriptionsResponse
        {
            VectorDescription = $"Placeholder vector description for '{login}'.",
            PersonaDescription = $"Placeholder persona description derived from '{bio}'."
        };

        return Task.FromResult(response);
    }
}
