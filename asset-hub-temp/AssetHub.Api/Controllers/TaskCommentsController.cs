using System;
using System.Collections.Generic;
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
[Route("api/v1/work-tasks/{taskId}/comments")]
[Authorize]
[RequirePlanLimits("tasks")]
public class TaskCommentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskCommentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult> GetTaskComments(Guid taskId)
    {
        var result = await _mediator.Send(new GetWorkTaskCommentsQuery(taskId));
        return Ok(new { items = result });
    }

    [HttpPost]
    public async Task<ActionResult<TaskCommentDto>> AddComment(Guid taskId, [FromBody] AddTaskCommentCommand command)
    {
        command.WorkTaskId = taskId;
        var id = await _mediator.Send(command);
        
        var commentDto = new TaskCommentDto
        {
            Id = id,
            Text = command.Text,
            CreatedAt = DateTime.UtcNow,
            CreatedByName = null
        };
        
        return CreatedAtAction(nameof(GetTaskComments), new { taskId }, commentDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateComment(Guid taskId, Guid id, [FromBody] UpdateTaskCommentCommand command)
    {
        command.CommentId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteComment(Guid taskId, Guid id)
    {
        var command = new DeleteTaskCommentCommand { CommentId = id };
        await _mediator.Send(command);
        return NoContent();
    }
}
