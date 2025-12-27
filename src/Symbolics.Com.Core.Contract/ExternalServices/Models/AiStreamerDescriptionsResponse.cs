namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed class AiStreamerDescriptionsResponse
{
    public string VectorDescription { get; set; } = string.Empty;
    public string PersonaDescription { get; set; } = string.Empty;
    public ConsumptionMetrics Consumption { get; set; } = new();
}
