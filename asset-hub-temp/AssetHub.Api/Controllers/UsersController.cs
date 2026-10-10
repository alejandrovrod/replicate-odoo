using System;
using System.Linq;
using System.Threading.Tasks;
using AssetHub.Application.Auth.Commands;
using AssetHub.Application.Users.Commands;
using AssetHub.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "permission:users:read")]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "permission:users:read")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var result = await _mediator.Send(new GetUserByIdQuery { UserId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetUserById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserCommand command)
    {
        command.UserId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        await _mediator.Send(new DeactivateUserCommand { UserId = id });
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        await _mediator.Send(new ActivateUserCommand { UserId = id });
        return NoContent();
    }

    [HttpPost("{id}/roles")]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        await _mediator.Send(new AssignUserRoleCommand { UserId = id, RoleId = request.RoleId });
        return NoContent();
    }

    [HttpDelete("{id}/roles/{roleId}")]
    [Authorize(Policy = "permission:users:manage")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        await _mediator.Send(new RemoveUserRoleCommand { UserId = id, RoleId = roleId });
        return NoContent();
    }

    // ---- Invitaciones ----

    [HttpGet("invitations")]
    [Authorize(Policy = "permission:users:read")]
    public async Task<IActionResult> GetInvitations([FromQuery] bool onlyPending = false)
    {
        var result = await _mediator.Send(new GetInvitationsQuery { OnlyPending = onlyPending });
        return Ok(result);
    }

    [HttpPost("invitations")]
    [Authorize(Policy = "permission:user:invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserCommand command)
    {
        var result = await _mediator.Send(command);
        // El token solo se expone al invitador (user:invite); el frontend arma
        // el link de aceptación para compartirlo con el invitado.
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            id = result.Id,
            token = result.Token,
            acceptUrl = $"{baseUrl}/accept-invitation?token={result.Token}"
        });
    }

    [HttpPost("invitations/{id}/resend")]
    [Authorize(Policy = "permission:user:invite")]
    public async Task<IActionResult> ResendInvitation(Guid id)
    {
        var result = await _mediator.Send(new ResendInvitationCommand { InvitationId = id });
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            id = result.Id,
            token = result.Token,
            acceptUrl = $"{baseUrl}/accept-invitation?token={result.Token}"
        });
    }

    [HttpPost("invitations/{id}/cancel")]
    [Authorize(Policy = "permission:user:invite")]
    public async Task<IActionResult> CancelInvitation(Guid id)
    {
        await _mediator.Send(new CancelInvitationCommand { InvitationId = id });
        return NoContent();
    }
}

public class AssignRoleRequest
{
    public Guid RoleId { get; set; }
}
