namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class StreamerTwitch
{
    public required string TwitchId { get; set; }
    public Guid StreamerId { get; set; }
    public required string TwitchLogin { get; set; }
    public required string TwitchName { get; set; }

    public Streamer? Streamer { get; set; }
}
