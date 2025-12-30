using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
            PersonaDescription = "persona",
            Email = "streamer@example.com",
            Languages = ["fr"],
            Consumption = new ConsumptionMetrics
            {
                Model = "gemini",
                InputUnits = 10,
                OutputUnits = 20,
                CachedUnits = 2,
                ProcessingTimeMs = 100
            }
        };
        var embeddingResponse = new EmbeddingResponse
        {
            Vector = [1f, 2f],
            Consumption = new ConsumptionMetrics
            {
                Model = "gemini-embedding",
                InputUnits = 4,
                OutputUnits = 2,
                CachedUnits = 0,
                ProcessingTimeMs = 50
            }
        };

        var worker = BuildWorker(
            streamerQueue: [streamerItem],
            twitchStreamerResponse: twitchInfo,
            aiStreamerResponse: aiResponse,
            embeddingResponse: embeddingResponse);

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.TwitchService.Verify(service => service.GetStreamerInfos("login-1"), Times.Once);
        worker.AiService.Verify(service => service.GenerateStreamerDescription(
            "twitch-1",
            "login-1",
            "Streamer Name",
            "https://www.twitch.tv/login-1",
            "bio"),
            Times.Once);
        worker.EmbeddingService.Verify(service => service.GenerateEmbedding("vector"), Times.Once);
        worker.QdrantClient.Verify(client => client.SaveStreamerDescription(streamerItem.StreamerId, embeddingResponse.Vector, "vector", It.Is<IReadOnlyCollection<string>>(languages => languages.SequenceEqual(new[] { "fr" }))), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.FinalizeStreamerEnrichmentAsync(
            It.IsAny<StreamerEnrichmentUpdate>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        worker.ConsumptionTracker.Verify(tracker => tracker.LogAsync(
            "Gemini",
            "GenerateStreamerDescription",
            "gemini",
            10,
            20,
            2,
            100),
            Times.Once);
        worker.ConsumptionTracker.Verify(tracker => tracker.LogAsync(
            "Gemini",
            "GenerateEmbedding",
            "gemini-embedding",
            4,
            2,
            0,
            50),
            Times.Once);
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
            VectorDescription = "game vector",
            Consumption = new ConsumptionMetrics
            {
                Model = "gemini",
                InputUnits = 11,
                OutputUnits = 22,
                CachedUnits = 3,
                ProcessingTimeMs = 120
            }
        };
        var embeddingResponse = new EmbeddingResponse
        {
            Vector = [3f, 4f],
            Consumption = new ConsumptionMetrics
            {
                Model = "gemini-embedding",
                InputUnits = 5,
                OutputUnits = 2,
                CachedUnits = 0,
                ProcessingTimeMs = 55
            }
        };

        var worker = BuildWorker(
            gameQueue: [gameItem],
            twitchGameResponse: twitchInfo,
            aiGameResponse: aiResponse,
            embeddingResponse: embeddingResponse);

        await worker.Worker.DoWorkAsync(CancellationToken.None);

        worker.TwitchService.Verify(service => service.GetGameInfos("game-1"), Times.Once);
        worker.AiService.Verify(service => service.GenerateGameDescription("game-1", "Game Name"), Times.Once);
        worker.EmbeddingService.Verify(service => service.GenerateEmbedding("game vector"), Times.Once);
        worker.QdrantClient.Verify(client => client.SaveGameDescription(gameItem.GameId, embeddingResponse.Vector, "game vector"), Times.Once);
        worker.WorkerRepository.Verify(repository => repository.FinalizeGameEnrichmentAsync(
            It.IsAny<GameEnrichmentUpdate>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        worker.ConsumptionTracker.Verify(tracker => tracker.LogAsync(
            "Gemini",
            "GenerateGameDescription",
            "gemini",
            11,
            22,
            3,
            120),
            Times.Once);
        worker.ConsumptionTracker.Verify(tracker => tracker.LogAsync(
            "Gemini",
            "GenerateEmbedding",
            "gemini-embedding",
            5,
            2,
            0,
            55),
            Times.Once);
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
        var consumptionTracker = new Mock<IConsumptionTracker>();
        var qdrantClient = new Mock<IQdrantClient>();
        var workerRepository = new Mock<IWorkerRepository>();
        var logger = new Mock<ILogger<TwitchEnrichmentWorker>>();

        var services = new ServiceCollection();
        services.AddSingleton(twitchService.Object);
        services.AddSingleton(aiService.Object);
        services.AddSingleton(embeddingService.Object);
        services.AddSingleton(consumptionTracker.Object);
        services.AddSingleton(qdrantClient.Object);
        services.AddSingleton(workerRepository.Object);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = new TestScopeFactory(serviceProvider);

        workerRepository.Setup(repository => repository.GetStreamerEnrichmentQueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(streamerQueue ?? []);
        workerRepository.Setup(repository => repository.GetGameEnrichmentQueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameQueue ?? []);
        workerRepository.Setup(repository => repository.FinalizeStreamerEnrichmentAsync(It.IsAny<StreamerEnrichmentUpdate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.FinalizeGameEnrichmentAsync(It.IsAny<GameEnrichmentUpdate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.IncrementStreamerRetryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        workerRepository.Setup(repository => repository.IncrementGameRetryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        twitchService.Setup(service => service.GetStreamerInfos(It.IsAny<string>()))
            .ReturnsAsync(twitchStreamerResponse ?? new TwitchStreamerResponse());
        twitchService.Setup(service => service.GetGameInfos(It.IsAny<string>()))
            .ReturnsAsync(twitchGameResponse ?? new TwitchGameResponse());
        aiService.Setup(service => service.GenerateStreamerDescription(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(aiStreamerResponse ?? new AiStreamerDescriptionsResponse());
        aiService.Setup(service => service.GenerateGameDescription(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(aiGameResponse ?? new AiGameDescriptionResponse());
        embeddingService.Setup(service => service.GenerateEmbedding(It.IsAny<string>()))
            .ReturnsAsync(embeddingResponse ?? new EmbeddingResponse());
        qdrantClient.Setup(client => client.SaveStreamerDescription(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(true);
        qdrantClient.Setup(client => client.SaveGameDescription(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<string>()))
            .Returns(true);

        var optionsMonitor = new TestOptionsMonitor<TwitchEnrichmentOptions>(new TwitchEnrichmentOptions
        {
            MaxConcurrentRequests = 1
        });

        var worker = new TwitchEnrichmentWorker(
            scopeFactory,
            optionsMonitor,
            logger.Object);

        return new WorkerHarness(worker, twitchService, aiService, embeddingService, consumptionTracker, qdrantClient, workerRepository);
    }

    private sealed record WorkerHarness(
        TwitchEnrichmentWorker Worker,
        Mock<ITwitchService> TwitchService,
        Mock<IAiService> AiService,
        Mock<IEmbeddingService> EmbeddingService,
        Mock<IConsumptionTracker> ConsumptionTracker,
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

    private sealed class TestScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public IServiceScope CreateScope() => new TestScope(_serviceProvider);

        private sealed class TestScope(IServiceProvider serviceProvider) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;

            public void Dispose()
            {
            }
        }
    }
}
