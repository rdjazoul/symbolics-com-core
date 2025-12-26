namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class StreamerEnrichmentQueue
{
    public Guid StreamerId { get; set; }
    public EnrichmentStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime AddedAt { get; set; }

    public Streamer? Streamer { get; set; }
}
