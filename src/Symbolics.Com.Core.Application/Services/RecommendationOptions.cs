namespace Symbolics.Com.Core.Application.Services;

public sealed class RecommendationOptions
{
    public int TopGamesLimit { get; set; } = 500;
    public int CacheTtlMinutes { get; set; } = 30;
    public double MinimumGamingRatio { get; set; } = 0.5;
}
