namespace Symbolics.Com.Core.Infrastructure.Qdrant;

public sealed class QdrantOptions
{
    public string UrlHttp { get; init; } = string.Empty;
    public string UrlGrpc { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
