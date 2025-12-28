namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class StreamMaintenanceOptions
{
    public TimeSpan RetentionDuration { get; set; } = TimeSpan.FromDays(30);
}
