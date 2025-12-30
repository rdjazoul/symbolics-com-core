namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class GameEnrichmentQueue
{
    public Guid GameId { get; set; }
    public EnrichmentStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? LastAttempt { get; set; }

    public Game? Game { get; set; }
}
