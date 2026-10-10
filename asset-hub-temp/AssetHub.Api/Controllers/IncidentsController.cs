using System;
using System.Threading.Tasks;
using AssetHub.Application.Incidents.Commands;
using AssetHub.Application.Incidents.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[RequirePlanLimits("incidents")] // Módulo incidents
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator, AssetHub.Application.Interfaces.ITenantDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    private readonly AssetHub.Application.Interfaces.ITenantDbContext _db;

    [HttpGet("fix")]
    [AllowAnonymous]
    public async Task<IActionResult> Fix()
    {
        var incidents = _db.Incidents.Where(i => i.State == "Resuelta" || i.State == "Cancelada" || i.State == "Cancelado" || i.State == "Cerrada" || i.State == "Closed" || i.State == "Resolved").ToList();
        foreach (var i in incidents)
        {
            i.ClosedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(default);
        return Ok(new { fixedCount = incidents.Count });
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? state, [FromQuery] Guid? assetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new SearchIncidentsQuery(q, state, null, assetId, page, pageSize));
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> AdvancedSearch([FromBody] AssetHub.Api.Controllers.AdvancedSearchRequest request)
    {
        request ??= new AssetHub.Api.Controllers.AdvancedSearchRequest();
        try {
            var result = await _mediator.Send(new SearchIncidentsQuery(request.SearchTerm, request.State, request.CatalogFilters, request.AssetId, request.Page, request.PageSize));
            return Ok(result);
        } catch (Exception ex) {
            return StatusCode(500, new { error = ex.ToString() });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetIncidentByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> ReportIncident([FromBody] ReportIncidentCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(ReportIncident), new { id }, new { id });
    }

    [HttpPatch("{id}/triage")]
    public async Task<IActionResult> TriageIncident(Guid id, [FromBody] TriageIncidentCommand command)
    {
        command.IncidentId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpPatch("{id}/state")]
    public async Task<IActionResult> ChangeState(Guid id, [FromBody] ChangeIncidentStateCommand command)
    {
        command.IncidentId = id;
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetTimeline(Guid id)
    {
        var result = await _mediator.Send(new GetIncidentTimelineQuery(id));
        return Ok(new { items = result });
    }
}
