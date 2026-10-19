using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Read-only CRM segmentation catalogs (loss reasons, territories, industries): seeded
/// rows served to the detail/close dropdowns. Reads write nothing, so no idempotency
/// guard applies (the board-read precedent).
/// </summary>
[ApiController]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class CrmCatalogsController : ControllerBase
{
    private readonly ICrmCatalogRepository _catalogs;

    public CrmCatalogsController(ICrmCatalogRepository catalogs)
    {
        _catalogs = catalogs;
    }

    /// <summary>Active loss reasons, alphabetical (ClosedLost dialog source).</summary>
    [HttpGet("api/v1/loss-reasons")]
    [Authorize(Policy = "permission:opportunity:read")]
    [ProducesResponseType(typeof(IReadOnlyList<CrmCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LossReasons(CancellationToken cancellationToken)
    {
        var rows = await _catalogs.ListLossReasonsAsync(cancellationToken);
        return Ok(rows.Select(r => new CrmCatalogDto(r.Id, r.Code, r.Name)).ToList());
    }

    /// <summary>Active territories, alphabetical (detail segmentation source).</summary>
    [HttpGet("api/v1/territories")]
    [Authorize(Policy = "permission:opportunity:read")]
    [ProducesResponseType(typeof(IReadOnlyList<CrmCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Territories(CancellationToken cancellationToken)
    {
        var rows = await _catalogs.ListTerritoriesAsync(cancellationToken);
        return Ok(rows.Select(r => new CrmCatalogDto(r.Id, r.Code, r.Name)).ToList());
    }

    /// <summary>Active industries, alphabetical (detail segmentation source).</summary>
    [HttpGet("api/v1/industries")]
    [Authorize(Policy = "permission:opportunity:read")]
    [ProducesResponseType(typeof(IReadOnlyList<CrmCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Industries(CancellationToken cancellationToken)
    {
        var rows = await _catalogs.ListIndustriesAsync(cancellationToken);
        return Ok(rows.Select(r => new CrmCatalogDto(r.Id, r.Code, r.Name)).ToList());
    }
}
