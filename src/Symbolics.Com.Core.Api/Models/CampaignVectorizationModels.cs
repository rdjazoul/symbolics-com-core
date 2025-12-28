namespace Symbolics.Com.Core.Api.Models;

public sealed record VectorizeCampaignRequest(string GameDescription, string CampaignDescription);

public sealed record VectorizeCampaignResponse(float[] Vector, string OptimizedDescription);
