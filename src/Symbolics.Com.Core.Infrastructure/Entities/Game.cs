namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class Game
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? VectorDescription { get; set; }

    public ICollection<GameTwitch> TwitchMappings { get; set; } = new List<GameTwitch>();
    public GameEnrichmentQueue? EnrichmentQueue { get; set; }
    public ICollection<GamePlayed> PlayedEntries { get; set; } = new List<GamePlayed>();
}
