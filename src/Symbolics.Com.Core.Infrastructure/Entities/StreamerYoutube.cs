namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class StreamerYoutube
{
    public Guid StreamerId { get; set; }
    public required string YoutubeUrl { get; set; }

    public Streamer? Streamer { get; set; }
}
