namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed class AiGameDescriptionResponse
{
    public string Description { get; set; } = string.Empty;
    public ConsumptionMetrics Consumption { get; set; } = new();
}
