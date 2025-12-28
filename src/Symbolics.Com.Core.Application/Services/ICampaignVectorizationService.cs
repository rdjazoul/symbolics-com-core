namespace Symbolics.Com.Core.Application.Services;

public interface ICampaignVectorizationService
{
    Task<CampaignVectorizationResult> VectorizeAsync(
        string gameDescription,
        string campaignDescription,
        CancellationToken cancellationToken);
}

public sealed record CampaignVectorizationResult(float[] Vector, string OptimizedDescription);
