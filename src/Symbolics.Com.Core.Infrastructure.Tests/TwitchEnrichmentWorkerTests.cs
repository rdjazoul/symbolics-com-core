using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.ExternalServices.Models;
using Symbolics.Com.Core.Contract.Qdrant;
using Symbolics.Com.Core.Infrastructure.Workers;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class TwitchEnrichmentWorkerTests
{
    [Fact]
    public async Task DoWorkAsync_CallsQueueRepositories()
    {
        var worker = BuildWorker();

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.WorkerRepository.Verify(repository => repository.GetStreamerEnrichmentQueueAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.GetGameEnrichmentQueueAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_ProcessesStreamerQueueEntry()
    {
        var streamerItem = new StreamerEnrichmentQueueItem(Guid.NewGuid(), "login-1");
        var twitchInfo = new TwitchStreamerResponse
        {
            Id = "twitch-1",
            Login = "login-1",
            DisplayName = "Streamer Name",
            Description = "bio"
        };
        var aiResponse = new AiStreamerDescriptionsResponse
        {
            VectorDescription = "vector",
            PersonaDescription = "persona"
        };
        var embeddingResponse = new EmbeddingResponse
        {
            Vector = [1f, 2f]
        };

        var worker = BuildWorker(
            streamerQueue: [streamerItem],
            twitchStreamerResponse: twitchInfo,
            aiStreamerResponse: aiResponse,
            embeddingResponse: embeddingResponse);

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.TwitchService.Verify(service => service.GetStreamerInfos("login-1"), Times.Once);
        worker.AiService.Verify(service => service.GenerateStreamerDescription("bio", "login-1"), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.UpdateStreamerEnrichmentAsync(
            It.IsAny<StreamerEnrichmentUpdate>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        worker.EmbeddingService.Verify(service => service.GenerateEmbedding("vector"), Times.Once);
        worker.QdrantClient.Verify(client => client.SaveStreamerDescription(streamerItem.StreamerId, embeddingResponse.Vector), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.RemoveStreamerFromEnrichmentQueueAsync(streamerItem.StreamerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_ProcessesGameQueueEntry()
    {
        var gameItem = new GameEnrichmentQueueItem(Guid.NewGuid(), "game-1");
        var twitchInfo = new TwitchGameResponse
        {
            Id = "game-1",
            Name = "Game Name"
        };
        var aiResponse = new AiGameDescriptionResponse
        {
            Description = "game vector"
        };
        var embeddingResponse = new EmbeddingResponse
        {
            Vector = [3f, 4f]
        };

        var worker = BuildWorker(
            gameQueue: [gameItem],
            twitchGameResponse: twitchInfo,
            aiGameResponse: aiResponse,
            embeddingResponse: embeddingResponse);

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.TwitchService.Verify(service => service.GetGameInfos("game-1"), Times.Once);
        worker.AiService.Verify(service => service.GenerateGameDescription("Game Name"), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.UpdateGameEnrichmentAsync(
            It.IsAny<GameEnrichmentUpdate>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        worker.EmbeddingService.Verify(service => service.GenerateEmbedding("game vector"), Times.Once);
        worker.QdrantClient.Verify(client => client.SaveGameDescription(gameItem.GameId, embeddingResponse.Vector), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.RemoveGameFromEnrichmentQueueAsync(gameItem.GameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WorkerHarness BuildWorker(
        IReadOnlyList<StreamerEnrichmentQueueItem>? streamerQueue = null,
        IReadOnlyList<GameEnrichmentQueueItem>? gameQueue = null,
        TwitchStreamerResponse? twitchStreamerResponse = null,
        TwitchGameResponse? twitchGameResponse = null,
        AiStreamerDescriptionsResponse? aiStreamerResponse = null,
        AiGameDescriptionResponse? aiGameResponse = null,
        EmbeddingResponse? embeddingResponse = null)
    {
        var twitchService = new Mock<ITwitchService>();
        var aiService = new Mock<IAiService>();
        var embeddingService = new Mock<IEmbeddingService>();
        var qdrantClient = new Mock<IQdrantClient>();
        var workerRepository = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<TwitchEnrichmentWorker>>();

        workerRepository.Setup(repository => repository.GetStreamerEnrichmentQueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(streamerQueue ?? []);
        workerRepository.Setup(repository => repository.GetGameEnrichmentQueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameQueue ?? []);
        workerRepository.Setup(repository => repository.UpdateStreamerEnrichmentAsync(It.IsAny<StreamerEnrichmentUpdate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.UpdateGameEnrichmentAsync(It.IsAny<GameEnrichmentUpdate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.RemoveStreamerFromEnrichmentQueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.RemoveGameFromEnrichmentQueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.IncrementStreamerRetryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.IncrementGameRetryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        twitchService.Setup(service => service.GetStreamerInfos(It.IsAny<string>()))
            .ReturnsAsync(twitchStreamerResponse ?? new TwitchStreamerResponse());
        twitchService.Setup(service => service.GetGameInfos(It.IsAny<string>()))
            .ReturnsAsync(twitchGameResponse ?? new TwitchGameResponse());
        aiService.Setup(service => service.GenerateStreamerDescription(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(aiStreamerResponse ?? new AiStreamerDescriptionsResponse());
        aiService.Setup(service => service.GenerateGameDescription(It.IsAny<string>()))
            .ReturnsAsync(aiGameResponse ?? new AiGameDescriptionResponse());
        embeddingService.Setup(service => service.GenerateEmbedding(It.IsAny<string>()))
            .ReturnsAsync(embeddingResponse ?? new EmbeddingResponse());
        qdrantClient.Setup(client => client.SaveStreamerDescription(It.IsAny<Guid>(), It.IsAny<float[]>()))
            .Returns(true);
        qdrantClient.Setup(client => client.SaveGameDescription(It.IsAny<Guid>(), It.IsAny<float[]>()))
            .Returns(true);

        var optionsMonitor = new TestOptionsMonitor<TwitchEnrichmentOptions>(new TwitchEnrichmentOptions
        {
            MaxConcurrentRequests = 1
        });

        var worker = new TwitchEnrichmentWorker(
            twitchService.Object,
            aiService.Object,
            embeddingService.Object,
            qdrantClient.Object,
            workerRepository.Object,
            optionsMonitor,
            logger.Object);

        return new WorkerHarness(worker, twitchService, aiService, embeddingService, qdrantClient, workerRepository);
    }

    private sealed record WorkerHarness(
        TwitchEnrichmentWorker Worker,
        Mock<ITwitchService> TwitchService,
        Mock<IAiService> AiService,
        Mock<IEmbeddingService> EmbeddingService,
        Mock<IQdrantClient> QdrantClient,
        Mock<IWorkerRepository> WorkerRepository);

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
