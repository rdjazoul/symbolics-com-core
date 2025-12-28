using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Symbolics.Com.Core.Contract.ExternalServices.Models;
using Symbolics.Com.Core.Infrastructure.ExternalServices;
using Xunit;

namespace Symbolics.Com.Core.Infrastructure.Tests;

public sealed class GeminiAiServiceTests
{
    [Fact]
    public async Task GenerateGameDescription_IncludesPromptVariables()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildGeminiEnvelope("desc"))
        });
        var service = BuildService(handler);

        await service.GenerateGameDescription("game-123", "Game Name");

        Assert.NotNull(handler.LastRequestContent);
        Assert.Contains("Game Name", handler.LastRequestContent);
        Assert.Contains("game-123", handler.LastRequestContent);
    }

    [Fact]
    public async Task GenerateStreamerDescription_ReturnsNullWhenEmailMissing()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("research"))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("{\"vector_description\":\"vector\",\"persona_description\":\"persona\",\"email\":null}"))
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var service = BuildService(handler);

        var response = await service.GenerateStreamerDescription(
            "twitch-1",
            "login",
            "Display Name",
            "https://twitch.tv/login",
            "bio");

        Assert.Null(response.Email);
    }

    [Fact]
    public async Task GenerateStreamerDescription_NormalizesEmail()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("research"))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("{\"vector_description\":\"vector\",\"persona_description\":\"persona\",\"email\":\"name [at] example dot com\"}"))
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var service = BuildService(handler);

        var response = await service.GenerateStreamerDescription(
            "twitch-1",
            "login",
            "Display Name",
            "https://twitch.tv/login",
            "bio");

        Assert.Equal("name@example.com", response.Email);
    }

    [Fact]
    public async Task GenerateStreamerDescription_AddsResearchConsumptionToStructuredConsumption()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("research", promptTokens: 3, candidateTokens: 5, cachedTokens: 1))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("{\"vector_description\":\"vector\",\"persona_description\":\"persona\",\"email\":null}", promptTokens: 7, candidateTokens: 11, cachedTokens: 2))
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var service = BuildService(handler);

        var response = await service.GenerateStreamerDescription(
            "twitch-1",
            "login",
            "Display Name",
            "https://twitch.tv/login",
            "bio");

        Assert.Equal(10, response.Consumption.InputUnits);
        Assert.Equal(16, response.Consumption.OutputUnits);
        Assert.Equal(3, response.Consumption.CachedUnits);
    }

    [Fact]
    public async Task GenerateStreamerDescription_PropagatesRateLimitErrors()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("rate limit")
        });
        var service = BuildService(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.GenerateStreamerDescription(
            "twitch-1",
            "login",
            "Display Name",
            "https://twitch.tv/login",
            "bio"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateGameDescription_ThrowsOnEmptyText()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(BuildGeminiEnvelope(string.Empty))
        });
        var service = BuildService(handler);

        await Assert.ThrowsAsync<AiResponseFormatException>(() => service.GenerateGameDescription("game-1", "Game Name"));
    }

    [Fact]
    public async Task GenerateStreamerDescription_ThrowsOnInvalidJson()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("research"))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildGeminiEnvelope("not-json"))
            }
        });
        var handler = new StubHttpMessageHandler(_ => responses.Dequeue());
        var service = BuildService(handler);

        await Assert.ThrowsAsync<AiResponseFormatException>(() => service.GenerateStreamerDescription(
            "twitch-1",
            "login",
            "Display Name",
            "https://twitch.tv/login",
            "bio"));
    }

    private static GeminiAiService BuildService(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = new AiOptions
        {
            DescriptionBaseUrl = "https://example.com/v1beta",
            DescriptionApiKey = "key",
            DescriptionModel = "gemini-1.5-pro"
        };
        var optionsMonitor = new TestOptionsMonitor<AiOptions>(options);
        var logger = new Mock<ILogger<GeminiAiService>>();

        return new GeminiAiService(httpClient, optionsMonitor, logger.Object);
    }

    private static string BuildGeminiEnvelope(
        string jsonContent,
        long promptTokens = 12,
        long candidateTokens = 34,
        long cachedTokens = 0)
    {
        var payload = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = jsonContent } }
                    }
                }
            },
            usageMetadata = new
            {
                promptTokenCount = promptTokens,
                candidatesTokenCount = candidateTokens,
                cachedContentTokenCount = cachedTokens
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
