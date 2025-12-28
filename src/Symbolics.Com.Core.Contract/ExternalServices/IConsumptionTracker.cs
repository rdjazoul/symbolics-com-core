namespace Symbolics.Com.Core.Contract.ExternalServices;

public interface IConsumptionTracker
{
    Task LogAsync(string service, string action, string model, long input, long output, long cached, long elapsedMs);
}
