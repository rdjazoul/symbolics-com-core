namespace Symbolics.Com.Core.Infrastructure.Entities;

public sealed class GameTwitch
{
    public required string TwitchId { get; set; }
    public Guid GameId { get; set; }
    public required string TwitchName { get; set; }

    public Game? Game { get; set; }
}
