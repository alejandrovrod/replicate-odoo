using System;
using System.Threading.Tasks;
using AssetHub.Application.Security.Commands;
using AssetHub.Application.Security.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "permission:roles:read")]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _mediator.Send(new GetRolesQuery());
        return Ok(result);
    }

    [HttpGet("permission-catalog")]
    [Authorize(Policy = "permission:roles:manage")]
    public async Task<IActionResult> GetPermissionCatalog()
    {
        var result = await _mediator.Send(new GetPermissionCatalogQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "permission:roles:read")]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var result = await _mediator.Send(new GetRoleByIdQuery { RoleId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "permission:roles:manage")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetRoleById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "permission:roles:manage")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleCommand command)
    {
        command.RoleId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "permission:roles:manage")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        await _mediator.Send(new DeleteRoleCommand { RoleId = id });
        return NoContent();
    }
}
