namespace Symbolics.Com.Core.Contract.ExternalServices.Models;

public record ConsumptionMetrics
{
    public string Model { get; init; } = string.Empty;
    public long InputUnits { get; init; }
    public long OutputUnits { get; init; }
    public long CachedUnits { get; init; }
    public int ProcessingTimeMs { get; init; }
    public long TotalUnits => InputUnits + OutputUnits + CachedUnits;
}
