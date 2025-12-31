namespace Symbolics.Com.Core.Application.Repositories;

public sealed record StreamerListingRow(
    Guid StreamerId,
    string TwitchId,
    string Language,
    bool HasEmail,
    string TwitchLogin,
    string TwitchName);
