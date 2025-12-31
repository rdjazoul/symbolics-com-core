namespace Symbolics.Com.Core.Application.Repositories;

public interface IStreamerRepository
{
    Task<PagedResult<StreamerListingRow>> GetStreamersAsync(
        string? language,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamerDetailsRow>> GetStreamerDetailsAsync(
        IEnumerable<Guid> streamerIds,
        CancellationToken cancellationToken = default);
}
