using System;
using System.Threading.Tasks;
using AssetHub.Application.Tasks.Commands;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/task-recurrences")]
[Authorize]
[RequirePlanLimits("tasks")]
public class TaskRecurrencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskRecurrencesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecurrence([FromBody] CreateTaskRecurrenceCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(CreateRecurrence), new { id }, new { id });
    }

    // Usually triggered by an internal worker or scheduler, but exposed here for testing/triggering manually
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateRecurrences()
    {
        await _mediator.Send(new EvaluateTaskRecurrencesCommand());
        return NoContent();
    }
}
