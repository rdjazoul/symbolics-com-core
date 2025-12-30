namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class GeminiOptions
{
    public string DescriptionBaseUrl { get; set; } = string.Empty;
    public string DescriptionApiKey { get; set; } = string.Empty;
    public string DescriptionModel { get; set; } = "gemini-1.5-pro";
    public string EmbeddingBaseUrl { get; set; } = string.Empty;
    public string EmbeddingApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public int EmbeddingDimensions { get; set; } = 1536;
    public decimal EmbeddingCostPerMillion { get; set; } = 0.025m;
    public decimal DescriptionInputCostPerMillion { get; set; } = 0.1m;
    public decimal DescriptionOutputCostPerMillion { get; set; } = 0.4m;
}
