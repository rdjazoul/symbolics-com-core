using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IAirtableService
{
    Task CreateRecommendationsAsync(
        IReadOnlyList<AirtableRecommendationRecord> records,
        CancellationToken cancellationToken = default);
}
