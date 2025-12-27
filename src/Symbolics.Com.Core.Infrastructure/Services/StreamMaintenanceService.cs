using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class StreamMaintenanceService(ILogger<StreamMaintenanceService> logger) : IStreamMaintenanceService
{
    private readonly ILogger<StreamMaintenanceService> _logger = logger;

    public Task CleanStreams(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stream cleanup triggered.");
        return Task.CompletedTask;
    }
}
