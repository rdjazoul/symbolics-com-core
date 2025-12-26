using Microsoft.Extensions.Hosting;

namespace Symbolics.Com.Core.Infrastructure.Qdrant;

public sealed class QdrantCollectionInitializer : IHostedService
{
    private readonly QdrantClient _qdrantClient;

    public QdrantCollectionInitializer(QdrantClient qdrantClient)
    {
        _qdrantClient = qdrantClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _qdrantClient.EnsureCollectionsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
