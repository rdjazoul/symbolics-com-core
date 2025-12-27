using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IConsumptionTracker _consumptionTracker;

    public EmbeddingService(
        HttpClient httpClient,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<EmbeddingService> logger,
        IConsumptionTracker consumptionTracker)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _consumptionTracker = consumptionTracker;
    }

    public async Task<EmbeddingResponse> GenerateEmbedding(string text)
    {
        var options = _optionsMonitor.CurrentValue;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation(
            "Generating embedding (placeholder). BaseUrl: {BaseUrl}, Length: {Length}",
            options.EmbeddingBaseUrl,
            text.Length);

        var dimension = options.EmbeddingDimensions > 0 ? options.EmbeddingDimensions : 1536;
        var vector = new float[dimension];
        stopwatch.Stop();

        var metrics = new ConsumptionMetrics
        {
            Model = "embedding-placeholder",
            InputUnits = text.Length,
            OutputUnits = dimension,
            ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
        };

        await _consumptionTracker.LogAsync(
            service: "Embedding",
            action: "Generate",
            model: metrics.Model,
            input: metrics.InputUnits,
            output: metrics.OutputUnits,
            elapsedMs: metrics.ProcessingTimeMs);

        return new EmbeddingResponse
        {
            Vector = vector,
            Consumption = metrics
        };
    }
}
