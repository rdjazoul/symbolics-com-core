using Symbolics.Com.Core.Contract.ExternalServices.Models;
using AiStreamerDescriptions = Symbolics.Com.Core.Contract.ExternalServices.Models.AiStreamerDescriptionsResponse;

namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IAiService
{
    Task<AiGameDescriptionResponse> GenerateGameDescription(string gameName);
    Task<AiStreamerDescriptions> GenerateStreamerDescription(string bio, string login);
}
