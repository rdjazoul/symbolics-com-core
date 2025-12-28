namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class ExternalServiceLog
{
    public Guid Id { get; set; }
    public string Service { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long InputUnits { get; set; }
    public long OutputUnits { get; set; }
    public long CachedUnits { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
