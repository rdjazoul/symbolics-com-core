using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Contract.Qdrant;

namespace Symbolics.Com.Core.Application.Recommendations;

public sealed class RecommendationProcessor(
    IQdrantClient qdrantClient,
    IRecommendationRepository recommendationRepository,
    ISystemSettingsRepository systemSettingsRepository) : IRecommendationProcessor
{
    private const string TopGamesLimitKey = "RecommendationTopGamesLimit";
    private const int DefaultTopGamesLimit = 500;
    private const int StreamerBatchSize = 200;
    private readonly IQdrantClient _qdrantClient = qdrantClient;
    private readonly IRecommendationRepository _recommendationRepository = recommendationRepository;
    private readonly ISystemSettingsRepository _systemSettingsRepository = systemSettingsRepository;

    public async Task<IReadOnlyList<MatchResult>> ProcessAsync(
        RecommendationQueueItem item,
        CancellationToken cancellationToken = default)
    {
        var topGamesLimit = await _systemSettingsRepository.GetIntSettingAsync(
            TopGamesLimitKey,
            DefaultTopGamesLimit,
            cancellationToken);

        var gameResults = await _qdrantClient.SearchGameVectorsAsync(item.Vector, topGamesLimit, cancellationToken);
        if (gameResults.Count == 0)
        {
            return Array.Empty<MatchResult>();
        }

        var gameScores = gameResults
            .Select(result => new
            {
                Parsed = Guid.TryParse(result.Id, out var id),
                Id = result.Id,
                Score = result.Score
            })
            .Where(entry => entry.Parsed)
            .Select(entry => new { GameId = Guid.Parse(entry.Id), entry.Score })
            .ToDictionary(entry => entry.GameId, entry => entry.Score);

        if (gameScores.Count == 0)
        {
            return Array.Empty<MatchResult>();
        }

        var streamers = await _recommendationRepository.GetStreamersByGamesAsync(
            gameScores.Keys.ToArray(),
            item.Language,
            cancellationToken);

        if (streamers.Count == 0)
        {
            return Array.Empty<MatchResult>();
        }

        var streamerSimilarity = await GetStreamerSimilaritiesAsync(item.Vector, streamers, cancellationToken);

        var results = streamers
            .Select(streamer =>
            {
                var averageGameSimilarity = streamer.GameIds
                    .Select(gameId => gameScores.TryGetValue(gameId, out var score) ? score : 0f)
                    .DefaultIfEmpty(0f)
                    .Average();

                streamerSimilarity.TryGetValue(streamer.StreamerId, out var similarityStreamer);

                var finalScore = (0.3 * similarityStreamer) + (0.7 * averageGameSimilarity);

                return new MatchResult(
                    streamer.StreamerId,
                    streamer.TwitchId,
                    streamer.Language,
                    streamer.HasEmail,
                    streamer.TwitchLogin,
                    streamer.TwitchName,
                    $"https://www.twitch.tv/{streamer.TwitchLogin}",
                    finalScore);
            })
            .OrderByDescending(result => result.FinalScore)
            .ToArray();

        return results;
    }

    private async Task<Dictionary<Guid, float>> GetStreamerSimilaritiesAsync(
        float[] vector,
        IReadOnlyList<StreamerMatchData> streamers,
        CancellationToken cancellationToken)
    {
        var streamerIds = streamers.Select(streamer => streamer.StreamerId).Distinct().ToArray();
        if (streamerIds.Length == 0)
        {
            return new Dictionary<Guid, float>();
        }

        var tasks = streamerIds
            .Chunk(StreamerBatchSize)
            .Select(batch => _qdrantClient.SearchStreamerVectorsAsync(vector, batch, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        return results
            .SelectMany(result => result)
            .Select(result => new
            {
                Parsed = Guid.TryParse(result.Id, out var id),
                Id = result.Id,
                result.Score
            })
            .Where(entry => entry.Parsed)
            .Select(entry => new { StreamerId = Guid.Parse(entry.Id), entry.Score })
            .GroupBy(entry => entry.StreamerId)
            .ToDictionary(group => group.Key, group => group.First().Score);
    }
}
