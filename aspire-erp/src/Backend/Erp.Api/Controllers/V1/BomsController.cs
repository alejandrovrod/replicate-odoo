using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Manufacturing.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// BOM read endpoints (Task 9.5): the recipe list and one recipe detail behind the BOM Studio
/// tree editor. Read-only by design (Constitution Article VI.1/VI.3 attributes, TenantMember
/// policy): NO create/update/deactivate routes exist because no BOM-CRUD command exists - BOM
/// rows are engineering masters seeded per test, and adding a write path is out of scope.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class BomsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BomsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Lists the company's BOMs with their lines and operations.</summary>
    /// <param name="companyId">Company that owns the recipes.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(ManufacturingErrorCodes.BomNotFound),
                ManufacturingErrorCodes.BomNotFound);
        }

        var boms = await _sender.SendAsync(new GetBomsQuery(companyId, page, pageSize), cancellationToken);
        return Ok(boms);
    }

    /// <summary>Loads one BOM with its lines and operations.</summary>
    /// <param name="id">BOM id.</param>
    /// <param name="companyId">Company that owns the recipe.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBom"),
                _errors.Text(ManufacturingErrorCodes.BomNotFound),
                ManufacturingErrorCodes.BomNotFound);
        }

        var bom = await _sender.SendAsync(new GetBomDetailQuery(companyId, id), cancellationToken);
        if (bom is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("BomNotFound"),
                _errors.Text(ManufacturingErrorCodes.BomNotFound),
                ManufacturingErrorCodes.BomNotFound);
        }

        return Ok(bom);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}
