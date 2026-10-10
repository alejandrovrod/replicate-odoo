using System;
using System.Threading.Tasks;
using AssetHub.Application.Tasks.Commands;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/work-tasks/{taskId}/evidences")]
[Authorize]
[RequirePlanLimits("tasks")]
public class TaskEvidencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskEvidencesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> AddEvidence(Guid taskId, [FromBody] AddTaskEvidenceCommand command)
    {
        command.WorkTaskId = taskId;
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(AddEvidence), new { taskId, id }, new { id });
    }
}
