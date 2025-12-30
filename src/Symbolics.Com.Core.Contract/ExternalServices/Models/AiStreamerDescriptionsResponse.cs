using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public sealed class AiStreamerDescriptionsResponse
{
    [JsonPropertyName("vector_description")]
    public string VectorDescription { get; set; } = string.Empty;

    [JsonPropertyName("persona_description")]
    public string PersonaDescription { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("language")]
    public List<string> Languages { get; set; } = new();

    public ConsumptionMetrics Consumption { get; set; } = new();
}
