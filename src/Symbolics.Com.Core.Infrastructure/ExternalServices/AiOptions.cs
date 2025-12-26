namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class AiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; } = 1536;
}
