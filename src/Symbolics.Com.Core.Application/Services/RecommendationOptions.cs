namespace Symbolics.Com.Core.Application.Services;

public sealed class RecommendationOptions
{
    public int TopGamesLimit { get; set; } = 500;
    public int CacheTtlMinutes { get; set; } = 30;
}
