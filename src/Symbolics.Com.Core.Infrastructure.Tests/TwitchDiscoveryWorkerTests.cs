using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;
using Symbolics.Com.Core.Infrastructure.Workers;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class TwitchDiscoveryWorkerTests
{
    [Fact]
    public async Task DoWorkAsync_CallsTwitchServiceWithCursor()
    {
        var response = BuildResponse("cursor-123");
        var worker = BuildWorker(
            workerState: new WorkerStateDto("TwitchDiscovery", "cursor-123", DateTime.UtcNow, true),
            response: response,
            existingStreamIds: Array.Empty<string>());

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.TwitchService.Verify(service => service.GetStreams("cursor-123"), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_CleansStreamsWhenCleanupIntervalElapsed()
    {
        var options = new TwitchDiscoveryOptions
        {
            CleanupInterval = TimeSpan.FromMinutes(30)
        };
        var lastCleanup = DateTime.UtcNow.AddHours(-2);
        var response = BuildResponse("cursor-clean");
        var worker = BuildWorker(
            workerState: new WorkerStateDto("TwitchDiscovery", null, lastCleanup, true),
            response: response,
            existingStreamIds: Array.Empty<string>(),
            options: options);

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.StreamMaintenanceService.Verify(service => service.CleanStreams(It.IsAny<CancellationToken>()), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.UpdateWorkerStateAsync(
            It.Is<WorkerStateDto>(state => state.LastCleanupDate > lastCleanup && state.CurrentCursor == "cursor-clean"),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task DoWorkAsync_AddsMissingStreamerAndGame()
    {
        var response = BuildResponse("cursor-add");
        var worker = BuildWorker(
            workerState: new WorkerStateDto("TwitchDiscovery", null, DateTime.UtcNow, true),
            response: response,
            existingStreamIds: Array.Empty<string>());

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.WorkerRepository.Verify(repository => repository.AddStreamersAsync(
            It.Is<IReadOnlyCollection<StreamerCreation>>(streamers => streamers.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
        worker.WorkerRepository.Verify(repository => repository.AddGamesAsync(
            It.Is<IReadOnlyCollection<GameCreation>>(games => games.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_SkipsGamePlayedWhenStreamExists()
    {
        var response = BuildResponse("cursor-existing");
        var worker = BuildWorker(
            workerState: new WorkerStateDto("TwitchDiscovery", null, DateTime.UtcNow, true),
            response: response,
            existingStreamIds: new[] { response.Data[0].Id });

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.WorkerRepository.Verify(repository => repository.AddGamePlayedBatchAsync(
            It.IsAny<IEnumerable<GamePlayedCreation>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_UpdatesCursorFromResponse()
    {
        var response = BuildResponse("cursor-next");
        var worker = BuildWorker(
            workerState: new WorkerStateDto("TwitchDiscovery", "cursor-old", DateTime.UtcNow, true),
            response: response,
            existingStreamIds: Array.Empty<string>());

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.WorkerRepository.Verify(repository => repository.UpdateWorkerStateAsync(
            It.Is<WorkerStateDto>(state => state.CurrentCursor == "cursor-next"),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private static TwitchStreamResponse BuildResponse(string cursor)
    {
        return new TwitchStreamResponse
        {
            Cursor = cursor,
            Data =
            [
                new TwitchStreamResponse
                {
                    Id = "stream-1",
                    UserId = "user-1",
                    UserLogin = "login-1",
                    UserName = "name-1",
                    GameId = "game-1",
                    GameName = "Game Name",
                    ViewerCount = 42,
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    Language = "fr"
                }
            ]
        };
    }

    private static WorkerHarness BuildWorker(
        WorkerStateDto workerState,
        TwitchStreamResponse response,
        IEnumerable<string> existingStreamIds,
        TwitchDiscoveryOptions? options = null)
    {
        var twitchService = new Mock<ITwitchService>();
        var workerRepository = new Mock<IWorkerRepository>();
        var streamMaintenanceService = new Mock<IStreamMaintenanceService>();
        var logger = new Mock<ILogger<TwitchDiscoveryWorker>>();

        workerRepository.Setup(repository => repository.TryAcquireLockAsync("TwitchDiscovery", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        workerRepository.Setup(repository => repository.GetWorkerStateAsync("TwitchDiscovery", It.IsAny<CancellationToken>()))
            .ReturnsAsync(workerState);
        workerRepository.Setup(repository => repository.GetExistingStreamIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingStreamIds.ToHashSet());
        workerRepository.Setup(repository => repository.GetStreamerIdsByTwitchIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>());
        workerRepository.Setup(repository => repository.GetGameIdsByTwitchIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>());
        workerRepository.Setup(repository => repository.AddStreamersAsync(It.IsAny<IEnumerable<StreamerCreation>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.AddGamesAsync(It.IsAny<IEnumerable<GameCreation>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.AddGamePlayedBatchAsync(It.IsAny<IEnumerable<GamePlayedCreation>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.UpdateWorkerStateAsync(It.IsAny<WorkerStateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        twitchService.Setup(service => service.GetStreams(It.IsAny<string?>()))
            .ReturnsAsync(response);

        var optionsMonitor = new TestOptionsMonitor<TwitchDiscoveryOptions>(options ?? new TwitchDiscoveryOptions());

        var worker = new TwitchDiscoveryWorker(
            twitchService.Object,
            workerRepository.Object,
            streamMaintenanceService.Object,
            optionsMonitor,
            logger.Object);

        return new WorkerHarness(worker, twitchService, workerRepository, streamMaintenanceService);
    }

    private sealed record WorkerHarness(
        TwitchDiscoveryWorker Worker,
        Mock<ITwitchService> TwitchService,
        Mock<IWorkerRepository> WorkerRepository,
        Mock<IStreamMaintenanceService> StreamMaintenanceService);

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
