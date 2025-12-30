namespace Symbolics.Com.Core.Application.Services;

public sealed record AdminGameUpdateResult(
    bool NotFound,
    string? OldIgdbId,
    string? NewIgdbId,
    bool DescriptionUpdated,
    bool GamingRatioUpdated);
