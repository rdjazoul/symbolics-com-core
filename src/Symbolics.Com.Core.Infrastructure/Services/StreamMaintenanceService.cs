using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class StreamMaintenanceService(
    IStreamRepository streamRepository,
    IOptionsMonitor<StreamMaintenanceOptions> optionsMonitor,
    ILogger<StreamMaintenanceService> logger) : IStreamMaintenanceService
{
    private readonly IStreamRepository _streamRepository = streamRepository;
    private readonly IOptionsMonitor<StreamMaintenanceOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<StreamMaintenanceService> _logger = logger;

    public async Task CleanStreams(CancellationToken cancellationToken = default)
    {
        var retentionDuration = _optionsMonitor.CurrentValue.RetentionDuration;
        var cutoffDate = DateTime.UtcNow.Subtract(retentionDuration);
        var deletedCount = await _streamRepository.DeleteStreamsOlderThanAsync(cutoffDate, cancellationToken);

        _logger.LogInformation(
            "Stream cleanup deleted {DeletedCount} entries older than {RetentionDuration}.",
            deletedCount,
            retentionDuration);
    }
}
