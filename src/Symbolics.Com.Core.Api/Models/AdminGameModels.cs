using System;
using System.Collections.Generic;

namespace Symbolics.Com.Core.Api.Models;

public sealed record AdminGameUpdateRequest(
    string? IgdbId,
    string? ManualDescription);

public sealed record AdminGameMissingIgdbResponse(
    Guid GameId,
    string? TwitchId,
    string Name,
    string? VectorDescription);
