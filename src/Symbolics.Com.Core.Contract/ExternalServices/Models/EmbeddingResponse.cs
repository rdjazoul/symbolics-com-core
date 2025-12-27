namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed class EmbeddingResponse
{
    public float[] Vector { get; set; } = [];
    public ConsumptionMetrics Consumption { get; set; } = new();
}
