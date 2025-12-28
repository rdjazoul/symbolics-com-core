using Symbolics.Com.Core.Contract.ExternalServices.Models;
using AiStreamerDescriptions = Symbolics.Com.Core.Contract.ExternalServices.Models.AiStreamerDescriptionsResponse;

namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IAiService
{
    Task<AiGameDescriptionResponse> GenerateGameDescription(string twitchGameId, string gameName);
    Task<AiStreamerDescriptions> GenerateStreamerDescription(
        string twitchId,
        string login,
        string displayName,
        string url,
        string rawDescription);
    Task<string> MergeAndOptimizeDescriptions(string gameDescription, string campaignDescription);
}
