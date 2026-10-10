using System;
using System.Threading.Tasks;
using AssetHub.Application.Analytics.Commands;
using AssetHub.Application.Analytics.Queries;
using AssetHub.Infrastructure.Billing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboards/stats")]
    [RequirePlanLimits("reports")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery());
        return Ok(result);
    }

    [HttpGet("assets/{id}/reliability")]
    [RequirePlanLimits("reports")]
    public async Task<IActionResult> GetAssetReliability(string id)
    {
        var assetId = ResolveAssetIdOrGlobal(id);
        if (assetId == Guid.Empty) return NotFound();

        var result = await _mediator.Send(new GetAssetReliabilityMetricsQuery { AssetId = assetId });
        return Ok(result);
    }

    [HttpGet("assets/{id}/tco")]
    [RequirePlanLimits("reports")]
    public async Task<IActionResult> GetAssetTco(string id, [FromQuery] bool includeSubtree = false)
    {
        var assetId = ResolveAssetIdOrGlobal(id);
        if (assetId == Guid.Empty) return NotFound();

        var result = await _mediator.Send(new GetAssetTcoQuery { AssetId = assetId, IncludeSubtree = includeSubtree });
        if (result == null) return NotFound();
        return Ok(result);
    }

    private static Guid? ResolveAssetIdOrGlobal(string id)
    {
        if (string.Equals(id, "global", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }

    [HttpPost("reports/export-costs")]
    [RequirePlanLimits("reports")]
    public async Task<IActionResult> ExportCostsReport([FromBody] GenerateCostsReportCommand command)
    {
        var jobId = await _mediator.Send(command);
        return Accepted(new { JobId = jobId, Message = "Report generation started. A link will be sent to the provided email." });
    }
}
