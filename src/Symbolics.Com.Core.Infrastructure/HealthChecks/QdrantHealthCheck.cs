using Microsoft.Extensions.Diagnostics.HealthChecks;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Infrastructure.HealthChecks;

public sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly IQdrantClient _qdrantClient;

    public QdrantHealthCheck(IQdrantClient qdrantClient)
    {
        _qdrantClient = qdrantClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isAlive = await _qdrantClient.CheckConnectivityAsync(cancellationToken);
        return isAlive
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Qdrant is unreachable.");
    }
}
