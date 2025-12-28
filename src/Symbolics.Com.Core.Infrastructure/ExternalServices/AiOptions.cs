namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class AiOptions
{
    public string DescriptionBaseUrl { get; set; } = string.Empty;
    public string DescriptionApiKey { get; set; } = string.Empty;
    public string DescriptionModel { get; set; } = "gemini-1.5-pro";
    public string EmbeddingBaseUrl { get; set; } = string.Empty;
    public string EmbeddingApiKey { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; } = 1536;
}
