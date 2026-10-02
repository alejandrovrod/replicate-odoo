using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Warehouses.Commands;
using Erp.Application.Features.Warehouses.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Warehouse endpoints (Task 3.1: "Warehouses enforce tree structure"). Attributes follow
/// Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1), content
/// negotiation (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// No [IdempotencyKeyRequired]: Article VI.4 scopes that guard to ledger-posting mutations and
/// creating a warehouse writes no GLEntry row (decision D7).
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class WarehousesController : ControllerBase
{
    private readonly ISender _sender;

    public WarehousesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's warehouse tree (roots at the top).</summary>
    /// <param name="companyId">Company that owns the warehouse tree.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseTreeNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTree([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                StockErrorCodes.WarehouseCompanyRequired);
        }

        var tree = await _sender.SendAsync(new GetWarehousesQuery(companyId), cancellationToken);
        return Ok(tree);
    }

    /// <summary>Creates one warehouse node. 201 with the created warehouse.</summary>
    /// <param name="command">Warehouse data (company, code, name, stock account, parent).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate code is a conflict, tree violations and everything else 400.
            return error.Code switch
            {
                StockErrorCodes.DuplicateWarehouseCode => Problem(
                    StatusCodes.Status409Conflict,
                    "Duplicate Warehouse Code",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Warehouse Validation Failed",
                    error.Message,
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetTree), new { companyId = dto.CompanyId }, dto);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
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
