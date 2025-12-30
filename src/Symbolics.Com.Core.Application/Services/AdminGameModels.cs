using System;

namespace Symbolics.Com.Core.Application.Services;

public sealed record AdminGameUpdateResult(
    bool NotFound,
    string? OldIgdbId,
    string? NewIgdbId,
    bool DescriptionUpdated,
    bool GamingRatioUpdated);

public sealed record GameMissingIgdbDto(
    Guid GameId,
    string? TwitchId,
    string Name,
    string? VectorDescription);
