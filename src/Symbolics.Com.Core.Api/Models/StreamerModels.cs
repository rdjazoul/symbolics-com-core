namespace Symbolics.Com.Core.Api.Models;

public sealed record StreamerResponse(
    Guid Id,
    string TwitchId,
    string Language,
    bool HasEmail,
    string TwitchLogin,
    string TwitchName,
    string UrlTwitch);

public sealed record StreamerListResponse(
    IReadOnlyList<StreamerResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
