namespace Symbolics.Com.Core.Application.Services;

public interface IAdminGameService
{
    Task<AdminGameUpdateResult> UpdateGameAsync(
        Guid gameId,
        string? igdbId,
        string? manualDescription,
        CancellationToken cancellationToken = default);
}
