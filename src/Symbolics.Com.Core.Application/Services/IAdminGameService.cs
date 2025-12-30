using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Symbolics.Com.Core.Application.Services;

public interface IAdminGameService
{
    Task<AdminGameUpdateResult> UpdateGameAsync(
        Guid gameId,
        string? igdbId,
        string? manualDescription,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameMissingIgdbDto>> GetGamesMissingIgdbAsync(
        CancellationToken cancellationToken = default);
}
