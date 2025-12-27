using Microsoft.AspNetCore.Mvc;
using Symbolics.Com.Core.Api.Models;
using Symbolics.Com.Core.Api.Security;
using Symbolics.Com.Core.Application.Workers;

namespace Symbolics.Com.Core.Api.Controllers;

[ApiController]
[Route("api/admin/workers")]
[AdminKey]
public sealed class AdminController(IWorkerRepository workerRepository) : ControllerBase
{
    private readonly IWorkerRepository _workerRepository = workerRepository;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminWorkerStateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetWorkers(CancellationToken cancellationToken)
    {
        var states = await _workerRepository.GetWorkerStatesAsync(cancellationToken);
        var response = states
            .Select(state => new AdminWorkerStateResponse(state.WorkerName, state.IsEnabled, state.LastCleanupDate))
            .ToList();

        return Ok(response);
    }

    [HttpPost("{workerName}/toggle")]
    [ProducesResponseType(typeof(AdminWorkerStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleWorker(
        string workerName,
        [FromBody] ToggleWorkerRequest request,
        CancellationToken cancellationToken)
    {
        var state = await _workerRepository.GetWorkerStateAsync(workerName, cancellationToken);
        if (state is null)
        {
            return NotFound();
        }

        var updatedState = state with { IsEnabled = request.IsEnabled };
        await _workerRepository.UpdateWorkerStateAsync(updatedState, cancellationToken);

        return Ok(new AdminWorkerStateResponse(
            updatedState.WorkerName,
            updatedState.IsEnabled,
            updatedState.LastCleanupDate));
    }
}
