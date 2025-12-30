namespace Symbolics.Com.Core.Application.Services;

public sealed record RecommendationJob(Guid SearchId, float[] Vector, string? Language);

public sealed record MatchResult(
    Guid Id,
    string TwitchId,
    string? Language,
    bool HasEmail,
    string TwitchLogin,
    string TwitchName,
    string UrlTwitch,
    double FinalScore,
    float GamingRatio);

public sealed record StreamerGameRow(
    Guid StreamerId,
    string? Language,
    bool HasEmail,
    string TwitchId,
    string TwitchLogin,
    string TwitchName,
    Guid GameId,
    float GamingRatio);

public static class RecommendationStatus
{
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public sealed record RecommendationCacheEntry(string Status, IReadOnlyList<MatchResult>? Results);
