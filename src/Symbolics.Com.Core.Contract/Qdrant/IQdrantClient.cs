namespace Symbolics.Com.Core.Contract.Qdrant;

public interface IQdrantClient
{
    bool SaveGameDescription(Guid gameId, float[] vector);
    bool SaveStreamerDescription(Guid streamerId, float[] vector);
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);
}
