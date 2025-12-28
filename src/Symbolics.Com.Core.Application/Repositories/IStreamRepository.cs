namespace Symbolics.Com.Core.Application.Repositories;

public interface IStreamRepository
{
    Task<int> DeleteStreamsOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
