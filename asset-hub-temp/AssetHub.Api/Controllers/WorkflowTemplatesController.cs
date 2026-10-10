using System;
using System.Threading.Tasks;
using AssetHub.Application.WorkflowTemplates.Commands;
using AssetHub.Application.WorkflowTemplates.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/workflow-templates")]
[Authorize]
public class WorkflowTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkflowTemplatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] bool includeInactive = false)
    {
        var result = await _mediator.Send(new SearchWorkflowTemplatesQuery(q, includeInactive));
        return Ok(new { items = result });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetWorkflowTemplateByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowTemplateCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowTemplateCommand command)
    {
        var commandWithId = command with { Id = id };
        await _mediator.Send(commandWithId);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteWorkflowTemplateCommand(id));
        return NoContent();
    }
}
