using Microsoft.AspNetCore.Mvc;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;

namespace RequestTracker.Api.Controllers;

[ApiController]
[Route("api/requests")]
public sealed class RequestsController(IRequestService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<RequestDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<RequestDto>>> GetPaged(
        [FromQuery] RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await service.GetPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RequestDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<RequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RequestDto>> UpdateStatus(
        int id, UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }

    // Always 200 for a valid request, even when every item failed: the outcome of each item is in the body.
    [HttpPatch("bulk-status")]
    [ProducesResponseType<BulkUpdateStatusResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BulkUpdateStatusResultDto>> BulkUpdateStatus(
        BulkUpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.BulkUpdateStatusAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType<PagedResult<StatusHistoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<StatusHistoryDto>>> GetHistory(
        int id, [FromQuery] StatusHistoryQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await service.GetHistoryAsync(id, parameters, cancellationToken);
        return Ok(result);
    }

    [HttpGet("stats")]
    [ProducesResponseType<RequestStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<RequestStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        var result = await service.GetStatsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("assignees")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetAssignees(CancellationToken cancellationToken)
    {
        var result = await service.GetAssigneesAsync(cancellationToken);
        return Ok(result);
    }
}
