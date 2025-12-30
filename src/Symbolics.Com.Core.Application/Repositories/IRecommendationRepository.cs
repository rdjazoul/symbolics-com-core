using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Application.Repositories;

public interface IRecommendationRepository
{
    Task<IReadOnlyList<StreamerGameRow>> GetStreamerGameRowsAsync(
        IReadOnlyCollection<Guid> gameIds,
        string? language,
        double minimumGamingRatio,
        CancellationToken cancellationToken = default);
}
