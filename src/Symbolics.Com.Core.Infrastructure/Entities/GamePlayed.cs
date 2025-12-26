namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class GamePlayed
{
    public Guid Id { get; set; }
    public Guid StreamerId { get; set; }
    public Guid GameId { get; set; }
    public int ViewerCount { get; set; }
    public DateTime Date { get; set; }
    public required string Language { get; set; }

    public Streamer? Streamer { get; set; }
    public Game? Game { get; set; }
}
