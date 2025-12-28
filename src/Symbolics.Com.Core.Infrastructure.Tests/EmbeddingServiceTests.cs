using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Infrastructure.ExternalServices;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class EmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbedding_BuildsRequestBody()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildEmbeddingEnvelope([1.2f, 3.4f], 7))
        });
        var service = BuildService(handler);

        await service.GenerateEmbedding("What is the meaning of life?");

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("\"task_type\":\"SEMANTIC_SIMILARITY\"", handler.LastRequestContent);
        Assert.Contains("\"output_dimensionality\":2", handler.LastRequestContent);
        Assert.Contains("\"model\":\"models/gemini-embedding-001\"", handler.LastRequestContent);
        Assert.Contains("What is the meaning of life?", handler.LastRequestContent);
    }

    [Fact]
    public async Task GenerateEmbedding_UsesPromptTokensInConsumption()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildEmbeddingEnvelope([1.2f, 3.4f], 11))
        });
        var service = BuildService(handler);

        var response = await service.GenerateEmbedding("vector text");

        Assert.Equal(11, response.Consumption.InputUnits);
        Assert.Equal(2, response.Consumption.OutputUnits);
        Assert.Equal("gemini-embedding-001", response.Consumption.Model);
    }

    [Fact]
    public async Task GenerateEmbedding_ThrowsOnEmptyText()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildEmbeddingEnvelope([1.2f, 3.4f], 11))
        });
        var service = BuildService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateEmbedding(" "));
    }

    private static EmbeddingService BuildService(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = new GeminiOptions
        {
            EmbeddingBaseUrl = "https://example.com/v1beta",
            EmbeddingApiKey = "key",
            EmbeddingModel = "gemini-embedding-001",
            EmbeddingDimensions = 2
        };
        var optionsMonitor = new TestOptionsMonitor<GeminiOptions>(options);
        var logger = new Mock<ILogger<EmbeddingService>>();

        return new EmbeddingService(httpClient, optionsMonitor, logger.Object);
    }

    private static string BuildEmbeddingEnvelope(float[] values, long promptTokenCount)
    {
        var payload = new
        {
            embedding = new
            {
                values
            },
            usageMetadata = new
            {
                promptTokenCount
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler = handler;

        public string? LastRequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _handler(request);
        }
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
