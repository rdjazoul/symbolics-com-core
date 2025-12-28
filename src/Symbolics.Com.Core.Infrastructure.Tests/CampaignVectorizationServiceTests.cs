using Microsoft.Extensions.Logging;
using Moq;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;
using Symbolics.Com.Core.Infrastructure.Services;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class CampaignVectorizationServiceTests
{
    [Fact]
    public async Task VectorizeAsync_CallsAiServiceWithDescriptions()
    {
        var aiService = new Mock<IAiService>();
        var embeddingService = new Mock<IEmbeddingService>();
        aiService.Setup(service => service.MergeAndOptimizeDescriptions("game", "campaign"))
            .ReturnsAsync("optimized");
        embeddingService.Setup(service => service.GenerateEmbedding("optimized"))
            .ReturnsAsync(new EmbeddingResponse { Vector = new float[1536] });
        var logger = new Mock<ILogger<CampaignVectorizationService>>();
        var service = new CampaignVectorizationService(aiService.Object, embeddingService.Object, logger.Object);

        await service.VectorizeAsync("game", "campaign", CancellationToken.None);

        aiService.Verify(service => service.MergeAndOptimizeDescriptions("game", "campaign"), Times.Once);
    }

    [Fact]
    public async Task VectorizeAsync_ReturnsVectorWithExpectedDimension()
    {
        var aiService = new Mock<IAiService>();
        var embeddingService = new Mock<IEmbeddingService>();
        aiService.Setup(service => service.MergeAndOptimizeDescriptions(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("optimized");
        embeddingService.Setup(service => service.GenerateEmbedding("optimized"))
            .ReturnsAsync(new EmbeddingResponse { Vector = new float[1536] });
        var logger = new Mock<ILogger<CampaignVectorizationService>>();
        var service = new CampaignVectorizationService(aiService.Object, embeddingService.Object, logger.Object);

        var result = await service.VectorizeAsync("game", "campaign", CancellationToken.None);

        Assert.Equal(1536, result.Vector.Length);
    }
}
