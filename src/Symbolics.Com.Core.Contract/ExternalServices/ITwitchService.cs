using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface ITwitchService
{
    Task<TwitchStreamResponse> GetStreams(string? cursor);
    Task<TwitchStreamerResponse> GetStreamerInfos(string twitchLogin);
    Task<TwitchGameResponse> GetGameInfos(string twitchGameId);
}
