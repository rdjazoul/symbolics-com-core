namespace Symbolics.Com.Core.Application.Recommendations;

public enum RecommendationStatus
{
    Processing,
    Completed
}

public sealed record RecommendationQueueItem(Guid SearchId, float[] Vector, string? Language);

public sealed record MatchResult(
    Guid Id,
    string TwitchId,
    string? Language,
    bool HasEmail,
    string TwitchLogin,
    string TwitchName,
    string UrlTwitch,
    double FinalScore);

public sealed record RecommendationCacheEntry(
    RecommendationStatus Status,
    IReadOnlyList<MatchResult> Results);

public sealed record RecommendationResponse(
    RecommendationStatus Status,
    IReadOnlyList<MatchResult> Results);

public sealed record StreamerMatchData(
    Guid StreamerId,
    string TwitchId,
    string TwitchLogin,
    string TwitchName,
    string? Language,
    bool HasEmail,
    IReadOnlyCollection<Guid> GameIds);
