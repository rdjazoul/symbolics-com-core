namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class Streamer
{
    public Guid Id { get; set; }
    public string? VectorDescription { get; set; }
    public string? PersonaDescription { get; set; }
    public string? Email { get; set; }
    public DateTime LastModificationDate { get; set; }

    public ICollection<StreamerTwitch> TwitchMappings { get; set; } = new List<StreamerTwitch>();
    public StreamerYoutube? Youtube { get; set; }
    public StreamerEnrichmentQueue? EnrichmentQueue { get; set; }
    public ICollection<GamePlayed> PlayedEntries { get; set; } = new List<GamePlayed>();
}
