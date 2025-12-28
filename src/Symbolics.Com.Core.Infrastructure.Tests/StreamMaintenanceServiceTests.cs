using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Infrastructure.Services;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class StreamMaintenanceServiceTests
{
    [Fact]
    public async Task CleanStreams_DeletesEntriesUsingRetentionDuration()
    {
        var retention = TimeSpan.FromDays(7);
        var optionsMonitor = new TestOptionsMonitor<StreamMaintenanceOptions>(new StreamMaintenanceOptions
        {
            RetentionDuration = retention
        });
        var repository = new Mock<IStreamRepository>();
        var logger = new Mock<ILogger<StreamMaintenanceService>>();

        repository.Setup(repo => repo.DeleteStreamsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = new StreamMaintenanceService(repository.Object, optionsMonitor, logger.Object);

        var beforeCall = DateTime.UtcNow;
        await service.CleanStreams();
        var afterCall = DateTime.UtcNow;

        var expectedLowerBound = beforeCall.Subtract(retention);
        var expectedUpperBound = afterCall.Subtract(retention);

        repository.Verify(
            repo => repo.DeleteStreamsOlderThanAsync(
                It.Is<DateTime>(cutoff => cutoff >= expectedLowerBound && cutoff <= expectedUpperBound),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
