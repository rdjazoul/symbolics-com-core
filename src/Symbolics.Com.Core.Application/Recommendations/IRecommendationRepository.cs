namespace Symbolics.Com.Core.Application.Recommendations;

public interface IRecommendationRepository
{
    Task<IReadOnlyList<StreamerMatchData>> GetStreamersByGamesAsync(
        IReadOnlyCollection<Guid> gameIds,
        string? language,
        CancellationToken cancellationToken = default);
}
