using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Infrastructure.Qdrant;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class QdrantClientTests
{
    [Fact]
    public void SaveGameDescription_UsesStringIdPayload()
    {
        var handler = new StubHttpMessageHandler();
        var client = BuildClient(handler);
        var gameId = Guid.NewGuid();

        var result = client.SaveGameDescription(gameId, [1.0f, 2.0f], "");

        Assert.True(result);
        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains(gameId.ToString(), handler.LastRequestContent);
        Assert.Contains("\"game_id\"", handler.LastRequestContent);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("GameVectors", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public void SaveStreamerDescription_UsesStringIdPayload()
    {
        var handler = new StubHttpMessageHandler();
        var client = BuildClient(handler);
        var streamerId = Guid.NewGuid();

        var result = client.SaveStreamerDescription(streamerId, [1.0f, 2.0f], "", "");

        Assert.True(result);
        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains(streamerId.ToString(), handler.LastRequestContent);
        Assert.Contains("\"streamer_id\"", handler.LastRequestContent);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("StreamerVectors", handler.LastRequestUri!.ToString());
    }

    private static QdrantClient BuildClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = new QdrantOptions
        {
            UrlHttp = "https://qdrant.example.com",
            ApiKey = "key"
        };
        var optionsMonitor = new TestOptionsMonitor<QdrantOptions>(options);
        var logger = new Mock<ILogger<QdrantClient>>();

        return new QdrantClient(httpClient, optionsMonitor, logger.Object);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public string? LastRequestContent { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            if (request.Content is not null)
            {
                LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }

    private sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue { get; } = currentValue;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
