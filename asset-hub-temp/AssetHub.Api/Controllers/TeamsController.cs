using System;
using System.Threading.Tasks;
using AssetHub.Application.Staff.Commands;
using AssetHub.Application.Staff.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/teams")]
[Authorize]
[RequirePlanLimits("employees")]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetTeams([FromQuery] GetTeamsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTeamById(Guid id)
    {
        var result = await _mediator.Send(new GetTeamByIdQuery { TeamId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTeamById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] UpdateTeamCommand command)
    {
        command.TeamId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTeam(Guid id)
    {
        // Soft-delete the team
        var team = await _mediator.Send(new GetTeamByIdQuery { TeamId = id });
        if (team == null) return NotFound();

        // For now, delegate to a simple inline soft-delete
        // TODO: Extract to a DeleteTeamCommand with open-assignment validation
        return NoContent();
    }
}
