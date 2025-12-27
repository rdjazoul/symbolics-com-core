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
    private readonly IConsumptionTracker _consumptionTracker;

    public GeminiAiService(
        HttpClient httpClient,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<GeminiAiService> logger,
        IConsumptionTracker consumptionTracker)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _consumptionTracker = consumptionTracker;
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
            stopwatch.Stop();

            var metrics = new ConsumptionMetrics
            {
                Model = "gemini-placeholder",
                InputUnits = gameName.Length,
                OutputUnits = description.Length,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };

            await _consumptionTracker.LogAsync(
                service: "Gemini",
                action: "GameDescription",
                model: metrics.Model,
                input: metrics.InputUnits,
                output: metrics.OutputUnits,
                elapsedMs: metrics.ProcessingTimeMs);

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

            stopwatch.Stop();

            response.Consumption = new ConsumptionMetrics
            {
                Model = "gemini-placeholder",
                InputUnits = bio.Length,
                OutputUnits = response.VectorDescription.Length + response.PersonaDescription.Length,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };

            await _consumptionTracker.LogAsync(
                service: "Gemini",
                action: "StreamerDescription",
                model: response.Consumption.Model,
                input: response.Consumption.InputUnits,
                output: response.Consumption.OutputUnits,
                elapsedMs: response.Consumption.ProcessingTimeMs);

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
