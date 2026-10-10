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
[Route("api/v1/employees")]
[Authorize]
[RequirePlanLimits("employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees([FromQuery] GetEmployeesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployeeById(Guid id)
    {
        var result = await _mediator.Send(new GetEmployeeByIdQuery { EmployeeId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(
            nameof(GetEmployeeById),
            new { id = result.Id },
            new { id = result.Id, userId = result.UserId, temporalPassword = result.TemporalPassword });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeCommand command)
    {
        command.EmployeeId = id;
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPatch("{id}/link-user")]
    public async Task<IActionResult> LinkUser(Guid id, [FromBody] LinkUserToEmployeeCommand command)
    {
        command.EmployeeId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPut("{id}/availability")]
    public async Task<IActionResult> SetAvailability(Guid id, [FromBody] SetEmployeeAvailabilityCommand command)
    {
        command.EmployeeId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateEmployee(Guid id)
    {
        var command = new DeactivateEmployeeCommand { EmployeeId = id };
        await _mediator.Send(command);
        return NoContent();
    }
}
