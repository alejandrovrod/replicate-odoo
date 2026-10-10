using System;
using System.Threading.Tasks;
using AssetHub.Application.CommunicationTemplates.Commands;
using AssetHub.Application.CommunicationTemplates.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/notification-mappings")]
public class NotificationMappingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationMappingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetNotificationMappingsQuery());
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMapping([FromBody] UpdateNotificationMappingCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(new { id = result });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMapping(Guid id)
    {
        var result = await _mediator.Send(new DeleteNotificationMappingCommand(id));
        if (!result) return NotFound();
        return NoContent();
    }
}
