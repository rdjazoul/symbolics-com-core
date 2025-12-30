namespace Symbolics.Com.Core.Infrastructure.Entities;

public enum EnrichmentStatus
{
    Pending = 0,
    InProcessing = 1,
    Completed = 2,
    RetryDelay = 3,
    Failed = 4,
    DataNotFound = 5,
    MissingIgdb = 6
}
