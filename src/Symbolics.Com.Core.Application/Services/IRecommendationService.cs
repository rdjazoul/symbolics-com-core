namespace Symbolics.Com.Core.Application.Services;

public interface IRecommendationService
{
    Task<IReadOnlyList<MatchResult>> BuildRecommendationsAsync(
        float[] vector,
        string? language,
        CancellationToken cancellationToken = default);
}
