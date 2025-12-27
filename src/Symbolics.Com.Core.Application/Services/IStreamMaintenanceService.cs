namespace Symbolics.Com.Core.Application.Services;

public interface IStreamMaintenanceService
{
    Task CleanStreams(CancellationToken cancellationToken = default);
}
