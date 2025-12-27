namespace Symbolics.Com.Core.Application.Workers;

public sealed record WorkerStateDto(string WorkerName, string? CurrentCursor, DateTime LastCleanupDate);

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
