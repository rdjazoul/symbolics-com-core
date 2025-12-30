using System.Collections.Generic;

namespace Symbolics.Com.Core.Contract.Qdrant;

public interface IQdrantClient
{
    bool SaveGameDescription(Guid gameId, float[] vector, string vectorDescription);
    bool SaveStreamerDescription(Guid streamerId, float[] vector, string vectorDescription, IReadOnlyCollection<string> languages);
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QdrantSearchResult>> SearchGameVectorsAsync(
        float[] vector,
        int limit,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QdrantSearchResult>> SearchStreamerVectorsAsync(
        float[] vector,
        int limit,
        CancellationToken cancellationToken = default);
}
