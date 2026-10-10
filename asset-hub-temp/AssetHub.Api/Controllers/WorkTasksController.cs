using System;
using System.Threading.Tasks;
using AssetHub.Application.Tasks.Commands;
using AssetHub.Application.Tasks.Dtos;
using AssetHub.Application.Tasks.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/work-tasks")]
[Authorize]
[RequirePlanLimits("tasks")]
public class WorkTasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkTasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<GetWorkTasksResult>> GetTasks(
        [FromQuery] string? state,
        [FromQuery] bool? assignedToMe,
        [FromQuery] Guid? assetId,
        [FromQuery] Guid? incidentId,
        [FromQuery] Guid? maintenanceOrderId,
        [FromQuery] Guid? preventivePlanId,
        [FromQuery] Guid? taskRecurrenceId,
        [FromQuery] string? search,
        [FromQuery] DateTime? dueBefore,
        [FromQuery] DateTime? dueAfter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetWorkTasksQuery
        {
            State = state,
            AssignedToMe = assignedToMe,
            AssetId = assetId,
            IncidentId = incidentId,
            MaintenanceOrderId = maintenanceOrderId,
            PreventivePlanId = preventivePlanId,
            TaskRecurrenceId = taskRecurrenceId,
            Search = search,
            DueBefore = dueBefore,
            DueAfter = dueAfter,
            Page = page,
            PageSize = pageSize
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkTaskDetailDto>> GetTaskById(Guid id)
    {
        var result = await _mediator.Send(new GetWorkTaskByIdQuery(id));
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateWorkTaskCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTaskById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WorkTaskDetailDto>> UpdateTask(Guid id, [FromBody] UpdateWorkTaskCommand command)
    {
        command.WorkTaskId = id;
        await _mediator.Send(command);
        var result = await _mediator.Send(new GetWorkTaskByIdQuery(id));
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _mediator.Send(new DeleteWorkTaskCommand(id));
        return NoContent();
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignTask(Guid id, [FromBody] AssignWorkTaskCommand command)
    {
        command.WorkTaskId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPut("{id}/state")]
    public async Task<ActionResult<WorkTaskDetailDto>> ChangeState(Guid id, [FromBody] ChangeWorkTaskStateCommand command)
    {
        command.WorkTaskId = id;
        await _mediator.Send(command);
        var result = await _mediator.Send(new GetWorkTaskByIdQuery(id));
        return Ok(result);
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult> GetTaskHistory(Guid id)
    {
        var result = await _mediator.Send(new GetWorkTaskHistoryQuery(id));
        return Ok(new { items = result });
    }

    [HttpPost("evaluate-due")]
    [Authorize(Roles = "Admin,System")]
    public async Task<ActionResult<EvaluateDueWorkTasksResult>> EvaluateDueTasks()
    {
        var result = await _mediator.Send(new EvaluateDueWorkTasksCommand());
        return Ok(result);
    }
}
