using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices.Models;
using Symbolics.Com.Core.Infrastructure.ExternalServices;

namespace Symbolics.Com.Core.Infrastructure.Services;

public sealed class DailyCostMonitor : IDailyCostMonitor
{
    private const decimal TokenUnit = 1_000_000m;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<GeminiOptions> _optionsMonitor;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _sync = new();
    private DateTime _cachedDay = DateTime.MinValue;
    private DailyCostTotals _cachedTotals = new(0, 0, 0);

    public DailyCostMonitor(IServiceScopeFactory scopeFactory, IOptionsMonitor<GeminiOptions> optionsMonitor)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<decimal> GetCurrentCostAsync(DateTime dayUtc, CancellationToken cancellationToken = default)
    {
        var totals = await GetTotalsAsync(dayUtc, cancellationToken);
        var options = _optionsMonitor.CurrentValue;

        var embeddingCost = totals.EmbeddingUnits / TokenUnit * options.EmbeddingCostPerMillion;
        var descriptionInputCost = totals.DescriptionInputUnits / TokenUnit * options.DescriptionInputCostPerMillion;
        var descriptionOutputCost = totals.DescriptionOutputUnits / TokenUnit * options.DescriptionOutputCostPerMillion;

        return embeddingCost + descriptionInputCost + descriptionOutputCost;
    }

    public void RegisterConsumption(string actionType, ConsumptionMetrics consumption)
    {
        lock (_sync)
        {
            if (_cachedDay == DateTime.MinValue)
            {
                return;
            }

            if (_cachedDay != DateTime.UtcNow.Date)
            {
                return;
            }

            switch (actionType)
            {
                case "GenerateEmbedding":
                    _cachedTotals = _cachedTotals with
                    {
                        EmbeddingUnits = _cachedTotals.EmbeddingUnits + consumption.InputUnits + consumption.OutputUnits
                    };
                    break;
                case "GenerateGameDescription":
                case "GenerateStreamerDescription":
                    _cachedTotals = _cachedTotals with
                    {
                        DescriptionInputUnits = _cachedTotals.DescriptionInputUnits + consumption.InputUnits,
                        DescriptionOutputUnits = _cachedTotals.DescriptionOutputUnits + consumption.OutputUnits
                    };
                    break;
            }
        }
    }

    private async Task<DailyCostTotals> GetTotalsAsync(DateTime dayUtc, CancellationToken cancellationToken)
    {
        var day = dayUtc.Date;
        lock (_sync)
        {
            if (_cachedDay == day)
            {
                return _cachedTotals;
            }
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            lock (_sync)
            {
                if (_cachedDay == day)
                {
                    return _cachedTotals;
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();
            var totals = await workerRepository.GetDailyCostTotalsAsync(day, cancellationToken);
            lock (_sync)
            {
                _cachedDay = day;
                _cachedTotals = totals;
            }

            return totals;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
