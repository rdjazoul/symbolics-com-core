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
    public GeminiAiService(
        HttpClient httpClient,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<AiGameDescriptionResponse> GenerateGameDescription(string gameName)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Generating game description (placeholder). BaseUrl: {BaseUrl}, Game: {GameName}",
                options.DescriptionBaseUrl,
                gameName);

            var description = $"Placeholder description for game '{gameName}'.";

            await Task.Delay(Random.Shared.Next(1000, 5000));

            stopwatch.Stop();

            var metrics = new ConsumptionMetrics
            {
                Model = "gemini-placeholder",
                InputUnits = gameName.Length,
                OutputUnits = description.Length,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };

            return new AiGameDescriptionResponse
            {
                Description = description,
                Consumption = metrics
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to generate game description for {GameName}.", gameName);
            throw;
        }
    }

    public async Task<AiStreamerDescriptionsResponse> GenerateStreamerDescription(string bio, string login)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation(
                "Generating streamer descriptions (placeholder). BaseUrl: {BaseUrl}, Login: {Login}",
                options.DescriptionBaseUrl,
                login);

            var response = new AiStreamerDescriptionsResponse
            {
                VectorDescription = $"Placeholder vector description for '{login}'.",
                PersonaDescription = $"Placeholder persona description derived from '{bio}'."
            };

            await Task.Delay(Random.Shared.Next(1000, 5000));

            stopwatch.Stop();

            response.Consumption = new ConsumptionMetrics
            {
                Model = "gemini-placeholder",
                InputUnits = bio.Length,
                OutputUnits = response.VectorDescription.Length + response.PersonaDescription.Length,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to generate streamer descriptions for {Login}.", login);
            throw;
        }
    }
}
