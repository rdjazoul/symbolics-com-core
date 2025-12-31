namespace Symbolics.Com.Core.Application.Repositories;

public sealed record StreamerListingRow(
    Guid StreamerId,
    string TwitchId,
    string Language,
    bool HasEmail,
    string TwitchLogin,
    string TwitchName);

public sealed record StreamerDetailsRow(
    Guid StreamerId,
    string? PersonaDescription,
    string? VectorDescription,
    string? Email);
