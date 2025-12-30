namespace Symbolics.Com.Core.Application.Services;

public interface IStreamerStatsService
{
    Task UpdateGamingRatioAsync(IReadOnlyCollection<Guid> streamerIds, CancellationToken cancellationToken = default);
}
