using System.Threading.Tasks;
using AssetHub.Application.Tenancy.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        // TODO: Agregar el filtro de Autorización [Authorize] en M3
        var result = await _mediator.Send(new GetCurrentTenantQuery());
        
        if (result == null) return NotFound();

        return Ok(new {
            id = result.Id,
            name = result.Name,
            slug = result.Slug,
            status = result.Status.ToString(),
            planId = result.PlanId,
            locale = result.Locale,
            timeZone = result.TimeZone
        });
    }

    [HttpGet("check-slug")]
    public async Task<IActionResult> CheckSlug([FromQuery] string slug)
    {
        var result = await _mediator.Send(new AssetHub.Application.Tenancy.Commands.CheckSlugCommand(slug));
        return Ok(result);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _mediator.Send(new AssetHub.Application.Tenants.Queries.GetTenantSettingsQuery());
        return Ok(result);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] AssetHub.Application.Tenants.Commands.UpdateTenantSettingsCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }
}
