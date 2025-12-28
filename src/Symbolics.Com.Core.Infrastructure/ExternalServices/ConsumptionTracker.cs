using Microsoft.Extensions.Logging;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Infrastructure.Entities;

namespace Symbolics.Com.Core.Infrastructure.ExternalServices;

public sealed class ConsumptionTracker : IConsumptionTracker
{
    private readonly IRepository<ExternalServiceLog> _repository;
    private readonly ILogger<ConsumptionTracker> _logger;

    public ConsumptionTracker(IRepository<ExternalServiceLog> repository, ILogger<ConsumptionTracker> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LogAsync(string service, string action, string model, long input, long output, long cached, long elapsedMs)
    {
        var log = new ExternalServiceLog
        {
            Id = Guid.NewGuid(),
            Service = service,
            ActionType = action,
            Model = model,
            InputUnits = input,
            OutputUnits = output,
            CachedUnits = cached,
            ExecutionTimeMs = (int)elapsedMs,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(log);

        _logger.LogInformation(
            "External consumption logged. Service: {Service}, Action: {Action}, Model: {Model}, Input: {InputUnits}, Output: {OutputUnits}, Cached: {CachedUnits}, ElapsedMs: {ElapsedMs}",
            service,
            action,
            model,
            input,
            output,
            cached,
            elapsedMs);
    }
}
