namespace Symbolics.Com.Core.Infrastructure.Workers;

public sealed class IgdbRefreshOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromDays(1);
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromDays(7);
    public int MaxConcurrentRequests { get; set; } = 4;
}
