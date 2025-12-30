using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Application.Services;

public sealed class RecommendationService(
    IQdrantClient qdrantClient,
    IRecommendationRepository recommendationRepository,
    IOptionsMonitor<RecommendationOptions> optionsMonitor,
    ILogger<RecommendationService> logger) : IRecommendationService
{
    private const double StreamerWeight = 0.3;
    private const double GameWeight = 0.7;
    private const int DefaultTopGamesLimit = 500;

    private readonly IQdrantClient _qdrantClient = qdrantClient;
    private readonly IRecommendationRepository _recommendationRepository = recommendationRepository;
    private readonly IOptionsMonitor<RecommendationOptions> _optionsMonitor = optionsMonitor;
    private readonly ILogger<RecommendationService> _logger = logger;

    public async Task<IReadOnlyList<MatchResult>> BuildRecommendationsAsync(
        float[] vector,
        string? language,
        CancellationToken cancellationToken = default)
    {
        if (vector is null || vector.Length == 0)
        {
            throw new ArgumentException("Vector cannot be empty.", nameof(vector));
        }

        var topGamesLimit = _optionsMonitor.CurrentValue.TopGamesLimit;
        if (topGamesLimit <= 0)
        {
            topGamesLimit = DefaultTopGamesLimit;
        }

        var gameSearchTask = _qdrantClient.SearchGameVectorsAsync(vector, topGamesLimit, cancellationToken);
        var streamerSearchTask = _qdrantClient.SearchStreamerVectorsAsync(vector, topGamesLimit, cancellationToken);

        await Task.WhenAll(gameSearchTask, streamerSearchTask);

        var gameResults = await gameSearchTask;
        if (gameResults.Count == 0)
        {
            _logger.LogInformation("No game vectors found for recommendation search.");
            return [];
        }

        var gameIds = gameResults.Select(result => result.Id).ToArray();
        var minimumGamingRatio = _optionsMonitor.CurrentValue.MinimumGamingRatio;
        var rows = await _recommendationRepository.GetStreamerGameRowsAsync(
            gameIds,
            language,
            minimumGamingRatio,
            cancellationToken);
        if (rows.Count == 0)
        {
            _logger.LogInformation("No streamers found after filtering by top games.");
            return [];
        }

        var gameScoreMap = gameResults.ToDictionary(result => result.Id, result => (double)result.Score);
        var streamerScoreMap = (await streamerSearchTask)
            .GroupBy(result => result.Id)
            .ToDictionary(group => group.Key, group => (double)group.First().Score);

        var results = rows
            .GroupBy(row => row.StreamerId)
            .Select(group => BuildMatch(group, gameScoreMap, streamerScoreMap))
            .OrderByDescending(result => result.FinalScore)
            .ToList();

        return results;
    }

    private static MatchResult BuildMatch(
        IGrouping<Guid, StreamerGameRow> group,
        IReadOnlyDictionary<Guid, double> gameScoreMap,
        IReadOnlyDictionary<Guid, double> streamerScoreMap)
    {
        var representative = group.First();
        var gameScores = group
            .Select(row => row.GameId)
            .Distinct()
            .Select(gameId => gameScoreMap.TryGetValue(gameId, out var score) ? score : 0d)
            .Where(score => score > 0d)
            .ToArray();

        var averageSimilarityGames = gameScores.Length == 0 ? 0d : gameScores.Average();
        var similarityStreamer = streamerScoreMap.TryGetValue(group.Key, out var score) ? score : 0d;
        var finalScore = (StreamerWeight * similarityStreamer) + (GameWeight * averageSimilarityGames);

        return new MatchResult(
            representative.StreamerId,
            representative.TwitchId,
            representative.Language,
            representative.HasEmail,
            representative.TwitchLogin,
            representative.TwitchName,
            BuildTwitchUrl(representative.TwitchLogin),
            finalScore,
            representative.GamingRatio);
    }

    private static string BuildTwitchUrl(string twitchLogin)
    {
        return $"https://www.twitch.tv/{twitchLogin}";
    }
}
