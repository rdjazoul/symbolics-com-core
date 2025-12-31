using System;
using System.Collections.Generic;

namespace Symbolics.Com.Core.Application.Repositories;

public interface IStreamerRepository
{
    Task<PagedResult<StreamerListingRow>> GetStreamersAsync(
        string? language,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, StreamerDetailsRow>> GetStreamerDetailsAsync(
        IReadOnlyCollection<Guid> streamerIds,
        CancellationToken cancellationToken = default);
}
