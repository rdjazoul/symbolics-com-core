using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Symbolics.Com.Core.Infrastructure.Logging;

namespace Symbolics.Com.Core.Infrastructure.HealthChecks;

public sealed class SeqHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LogOptions> _optionsMonitor;

    public SeqHealthCheck(IHttpClientFactory httpClientFactory, IOptionsMonitor<LogOptions> optionsMonitor)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var seqUrl = _optionsMonitor.CurrentValue.SeqUrl;
        var healthUrl = _optionsMonitor.CurrentValue.SeqHealthUrl;

        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            healthUrl = seqUrl;
        }

        if (string.IsNullOrWhiteSpace(healthUrl))
        {
            return HealthCheckResult.Degraded("Seq URL is not configured.");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var response = await httpClient.GetAsync(healthUrl, cancellationToken);

            if (response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Degraded($"Seq returned status code {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Seq is unreachable.", ex);
        }
    }
}
