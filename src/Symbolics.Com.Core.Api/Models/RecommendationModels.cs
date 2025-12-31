using Symbolics.Com.Core.Application.Services;

namespace Symbolics.Com.Core.Api.Models;

public sealed record RecommendationRequest(float[] Vector, string? Language);

public sealed record ExpressRecommendationRequest(string Description, string? Language);

public sealed record RecommendationAcceptedResponse(Guid SearchId);

public sealed record RecommendationStatusResponse(string Status);

public sealed record AdminRecommendationResponse(
    int SentCount,
    IReadOnlyList<MatchResult> Recommendations);
