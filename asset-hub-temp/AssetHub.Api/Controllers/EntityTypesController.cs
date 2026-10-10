using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssetHub.Application.EntityTypes.Commands;
using AssetHub.Application.EntityTypes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/entity-types")]
[Authorize]
public class EntityTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EntityTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetEntityTypesQuery());
        return Ok(new { items = result });
    }

    [HttpPost]
    [Authorize(Policy = "permission:entity-types:create")]
    public async Task<IActionResult> Create([FromBody] CreateEntityTypeRequest request)
    {
        var id = await _mediator.Send(new CreateEntityTypeCommand(
            request.Code, 
            request.Name, 
            request.Description, 
            request.Icon, 
            request.EnabledModules, 
            request.DefaultCatalogIds
        ));
        return Created($"/api/v1/entity-types/{request.Code}", new { id });
    }

    [HttpPut("{code}")]
    [Authorize(Policy = "permission:entity-types:update")]
    public async Task<IActionResult> Update([FromRoute] string code, [FromBody] UpdateEntityTypeRequest request)
    {
        var success = await _mediator.Send(new UpdateEntityTypeCommand(
            code,
            request.Name,
            request.Description,
            request.Icon,
            request.EnabledModules,
            request.DefaultCatalogIds
        ));

        if (!success) return NotFound();
        return NoContent();
    }

    [HttpDelete("{code}")]
    [Authorize(Policy = "permission:entity-types:delete")]
    public async Task<IActionResult> Delete([FromRoute] string code)
    {
        var success = await _mediator.Send(new DeleteEntityTypeCommand(code));
        if (!success) return NotFound();
        return NoContent();
    }
}

public class CreateEntityTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string> EnabledModules { get; set; } = new();
    public List<Guid> DefaultCatalogIds { get; set; } = new();
}

public class UpdateEntityTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<string> EnabledModules { get; set; } = new();
    public List<Guid> DefaultCatalogIds { get; set; } = new();
}
