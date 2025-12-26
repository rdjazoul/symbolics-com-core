using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Symbolics.Com.Core.Infrastructure.Qdrant;

public sealed class QdrantCollectionInitializer : IHostedService
{
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<QdrantCollectionInitializer> _logger;

    public QdrantCollectionInitializer(QdrantClient qdrantClient, ILogger<QdrantCollectionInitializer> logger)
    {
        _qdrantClient = qdrantClient;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var cycleId = Guid.NewGuid();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CycleId"] = cycleId
        });

        _logger.LogInformation("Starting Qdrant collection initialization.");

        try
        {
            await _qdrantClient.EnsureCollectionsAsync(cancellationToken);
            _logger.LogInformation("Qdrant collection initialization completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant collection initialization failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
