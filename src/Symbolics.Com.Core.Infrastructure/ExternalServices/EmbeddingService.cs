using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Contract.ExternalServices;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(HttpClient httpClient, IOptionsMonitor<AiOptions> optionsMonitor, ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public Task<float[]> GenerateEmbedding(string text)
    {
        var options = _optionsMonitor.CurrentValue;
        _logger.LogInformation(
            "Generating embedding (placeholder). BaseUrl: {BaseUrl}, Length: {Length}",
            options.EmbeddingBaseUrl,
            text.Length);

        var dimension = options.EmbeddingDimensions > 0 ? options.EmbeddingDimensions : 1536;
        return Task.FromResult(new float[dimension]);
    }
}
