namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class GeminiOptions
{
    public string EmbeddingBaseUrl { get; set; } = string.Empty;
    public string EmbeddingApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; set; } = 1536;
}
