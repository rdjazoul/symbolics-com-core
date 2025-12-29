namespace Symbolics.Com.Core.Application.Recommendations;

public interface IRecommendationService
{
    Task<Guid> SubmitAsync(float[] vector, string? language, CancellationToken cancellationToken = default);
    Task<RecommendationResponse?> GetAsync(Guid searchId, CancellationToken cancellationToken = default);
}
