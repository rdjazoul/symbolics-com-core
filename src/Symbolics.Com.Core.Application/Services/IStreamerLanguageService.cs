namespace Symbolics.Com.Core.Application.Services;

public interface IStreamerLanguageService
{
    Task UpdateStreamerLanguageAsync(Guid streamerId, CancellationToken cancellationToken = default);
}
