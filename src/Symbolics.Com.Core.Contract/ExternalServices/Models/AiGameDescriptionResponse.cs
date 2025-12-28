using System.Text.Json.Serialization;

namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed class AiGameDescriptionResponse
{
    [JsonPropertyName("vector_description")]
    public string VectorDescription { get; set; } = string.Empty;
    public ConsumptionMetrics Consumption { get; set; } = new();
}
