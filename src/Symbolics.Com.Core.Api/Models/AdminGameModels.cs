namespace Symbolics.Com.Core.Api.Models;

public sealed record AdminGameUpdateRequest(
    string? IgdbId,
    string? ManualDescription);
