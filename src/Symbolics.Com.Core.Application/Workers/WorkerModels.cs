namespace Symbolics.Com.Core.Application.Workers;

public sealed record WorkerStateDto(string WorkerName, string? CurrentCursor, DateTime LastCleanupDate, bool IsEnabled);

public sealed record StreamerCreation(
    Guid StreamerId,
    string TwitchId,
    string TwitchLogin,
    string TwitchName);

public sealed record GameCreation(
    Guid GameId,
    string TwitchId,
    string TwitchName,
    string GameName);

public sealed record GamePlayedCreation(
    Guid GamePlayedId,
    Guid StreamerId,
    Guid GameId,
    int ViewerCount,
    DateTime Date,
    string Language,
    string TwitchStreamId);

public sealed record StreamerEnrichmentQueueItem(
    Guid StreamerId,
    string TwitchLogin);

public sealed record GameEnrichmentQueueItem(
    Guid GameId,
    string TwitchId);

public sealed record StreamerEnrichmentUpdate(
    Guid StreamerId,
    string TwitchId,
    string TwitchLogin,
    string TwitchName,
    string VectorDescription,
    string PersonaDescription,
    string? Email,
    string? Language,
    DateTime LastModificationDate);

public sealed record GameEnrichmentUpdate(
    Guid GameId,
    string TwitchId,
    string TwitchName,
    string VectorDescription);
