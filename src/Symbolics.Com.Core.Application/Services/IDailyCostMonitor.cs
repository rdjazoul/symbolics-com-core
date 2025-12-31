using Symbolics.Com.Core.Contract.ExternalServices.Models;

namespace Symbolics.Com.Core.Application.Services;

public interface IDailyCostMonitor
{
    Task<decimal> GetCurrentCostAsync(DateTime dayUtc, CancellationToken cancellationToken = default);

    void RegisterConsumption(string actionType, ConsumptionMetrics consumption);
}
