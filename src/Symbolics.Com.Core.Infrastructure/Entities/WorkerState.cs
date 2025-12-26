namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class WorkerState
{
    public required string WorkerName { get; set; }
    public string? CurrentCursor { get; set; }
    public DateTime LastCleanupDate { get; set; }
}
