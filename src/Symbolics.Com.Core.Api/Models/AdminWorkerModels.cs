namespace Symbolics.Com.Core.Api.Models;

public sealed record AdminWorkerStateResponse(
    string WorkerName,
    bool IsEnabled,
    DateTime LastCleanupDate);

public sealed record ToggleWorkerRequest(bool IsEnabled);
