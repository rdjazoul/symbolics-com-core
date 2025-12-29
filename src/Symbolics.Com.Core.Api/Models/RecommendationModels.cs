using Symbolics.Com.Core.Application.Recommendations;

namespace Symbolics.Com.Core.Api.Models;

public sealed record RecommendationRequest(float[] Vector, string? Language);

public sealed record RecommendationAcceptedResponse(Guid SearchId);

public sealed record RecommendationStatusResponse(RecommendationStatus Status, IReadOnlyList<MatchResult> Results);
