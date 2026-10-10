using System;
using System.Threading.Tasks;
using AssetHub.Api.Configuration;
using AssetHub.Application.Maintenance.Commands;
using AssetHub.Application.Maintenance.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/preventive-plans")]
[Authorize]
public class PreventivePlansController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOptions<SchedulerSettings> _schedulerSettings;

    public PreventivePlansController(IMediator mediator, IOptions<SchedulerSettings> schedulerSettings)
    {
        _mediator = mediator;
        _schedulerSettings = schedulerSettings;
    }

    [HttpGet]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> GetPreventivePlans([FromQuery] Guid? assetId, [FromQuery] Guid? templateId, [FromQuery] bool? active)
    {
        var result = await _mediator.Send(new GetPreventivePlansQuery
        {
            AssetId = assetId,
            TemplateId = templateId,
            Active = active
        });
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> GetPreventivePlanById(Guid id)
    {
        var plan = await _mediator.Send(new GetPreventivePlanByIdQuery { Id = id });
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpPost]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> CreatePreventivePlan([FromBody] CreatePreventivePlanCommand command)
    {
        var id = await _mediator.Send(command);
        var plan = await _mediator.Send(new GetPreventivePlanByIdQuery { Id = id });
        return CreatedAtAction(nameof(GetPreventivePlanById), new { id }, new { id, plan!.NextRunAt });
    }

    [HttpPut("{id:guid}")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> UpdatePreventivePlan(Guid id, [FromBody] UpdatePreventivePlanCommand command)
    {
        command.Id = id;
        var plan = await _mediator.Send(command);
        return Ok(plan);
    }

    [HttpDelete("{id:guid}")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> DeletePreventivePlan(Guid id)
    {
        await _mediator.Send(new DeletePreventivePlanCommand { Id = id });
        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle-active")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var plan = await _mediator.Send(new TogglePreventivePlanActiveCommand { Id = id });
        return Ok(plan);
    }

    [HttpGet("{id:guid}/logs")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> GetExecutionLogs(
        Guid id,
        [FromQuery] Guid? assetId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPreventivePlanExecutionLogsQuery
        {
            PlanId = id,
            AssetId = assetId,
            Status = status,
            Page = page,
            PageSize = pageSize
        });
        return Ok(result);
    }

    [HttpGet("{id:guid}/next-occurrences")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> GetNextOccurrences(Guid id, [FromQuery] int count = 12)
    {
        var result = await _mediator.Send(new GetPreventivePlanNextOccurrencesQuery
        {
            PlanId = id,
            Count = count
        });
        return Ok(result);
    }

    [HttpPost("{id:guid}/evaluate")]
    [RequirePlanLimits("preventive-plans")]
    public async Task<IActionResult> EvaluatePlan(Guid id)
    {
        var result = await _mediator.Send(new EvaluatePreventivePlanCommand { PlanId = id });
        return Ok(result);
    }

    /// <summary>
    /// Evaluates overdue preventive plans across all tenants.
    /// This endpoint is intended to be invoked by an external cron job and is protected by an API key.
    /// </summary>
    [HttpPost("evaluate-all")]
    [AllowAnonymous]
    public async Task<IActionResult> EvaluateAllPlans()
    {
        var providedKey = Request.Headers["X-Api-Key"].ToString();
        if (!string.Equals(providedKey, _schedulerSettings.Value.ApiKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "Invalid or missing scheduler API key." });
        }

        var result = await _mediator.Send(new EvaluatePreventivePlansCommand());
        return Ok(result);
    }
}
