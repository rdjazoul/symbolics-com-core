namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchEnrichmentOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int StreamerMaxRetryCount { get; set; } = 3;
    public int GameMaxRetryCount { get; set; } = 3;
    public int MaxConcurrentRequests { get; set; } = 4;
}
