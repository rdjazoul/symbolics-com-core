using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Contract.ExternalServices;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class CampaignVectorizationService(
    IAiService aiService,
    IEmbeddingService embeddingService,
    ILogger<CampaignVectorizationService> logger) : ICampaignVectorizationService
{
    private const int ExpectedVectorSize = 1536;
    private readonly IAiService _aiService = aiService;
    private readonly IEmbeddingService _embeddingService = embeddingService;
    private readonly ILogger<CampaignVectorizationService> _logger = logger;

    public async Task<CampaignVectorizationResult> VectorizeAsync(
        string gameDescription,
        string campaignDescription,
        CancellationToken cancellationToken)
    {
        var optimizedDescription = await _aiService.MergeAndOptimizeDescriptions(gameDescription, campaignDescription);
        var embedding = await _embeddingService.GenerateEmbedding(optimizedDescription);

        if (embedding.Vector.Length != ExpectedVectorSize)
        {
            _logger.LogError(
                "Embedding vector length {VectorLength} does not match expected {ExpectedLength}.",
                embedding.Vector.Length,
                ExpectedVectorSize);
            throw new InvalidOperationException("Embedding vector length does not match expected size.");
        }

        return new CampaignVectorizationResult(embedding.Vector, optimizedDescription);
    }
}
