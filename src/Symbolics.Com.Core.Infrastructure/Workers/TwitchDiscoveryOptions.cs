namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class TwitchDiscoveryOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
