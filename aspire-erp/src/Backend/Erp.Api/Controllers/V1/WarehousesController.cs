using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Warehouses.Commands;
using Erp.Application.Features.Warehouses.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public WarehousesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's warehouse tree (roots at the top).</summary>
    /// <param name="companyId">Company that owns the warehouse tree.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("tree")]
    [Authorize(Policy = "permission:warehouse:read")]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseTreeNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTree([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(StockErrorCodes.WarehouseCompanyRequired),
                StockErrorCodes.WarehouseCompanyRequired);
        }

        var tree = await _sender.SendAsync(new GetWarehousesQuery(companyId), cancellationToken);
        return Ok(tree);
    }

    /// <summary>
    /// Returns the company's warehouses as a flat paged list for card grids (Standard Pagination
    /// Pattern) - the companion to the hierarchical tree read above. <c>leavesOnly</c> restricts
    /// to ledger warehouses (groups hold no stock); <c>isActive</c> optionally filters by status.
    /// </summary>
    /// <param name="companyId">Company that owns the warehouses.</param>
    /// <param name="leavesOnly">When true, only non-group (ledger) warehouses.</param>
    /// <param name="isActive">Optional status filter.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Page size, 1..500 (default 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:warehouse:read")]
    [ProducesResponseType(typeof(PagedResult<WarehouseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] bool leavesOnly = false,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(StockErrorCodes.WarehouseCompanyRequired),
                StockErrorCodes.WarehouseCompanyRequired);
        }

        var warehouses = await _sender.SendAsync(
            new GetFlatWarehousesQuery(companyId, leavesOnly, isActive, page, pageSize),
            cancellationToken);
        return Ok(warehouses);
    }

    /// <summary>Creates one warehouse node. 201 with the created warehouse.</summary>
    /// <param name="command">Warehouse data (company, code, name, stock account, parent).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:warehouse:write")]
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
                    _common.Text("WarehouseConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("WarehouseRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetTree), new { companyId = dto.CompanyId }, dto);
    }

    /// <summary>Updates an existing warehouse node. 200 with the updated warehouse.</summary>
    /// <param name="id">Warehouse ID.</param>
    /// <param name="command">Updated warehouse data including RowVersion.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "permission:warehouse:write")]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidWarehouseId"),
                _errors.Text(StockErrorCodes.WarehouseNotFound),
                StockErrorCodes.WarehouseNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            return error.Code switch
            {
                StockErrorCodes.WarehouseNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("WarehouseNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict or StockErrorCodes.DuplicateWarehouseCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("WarehouseConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("WarehouseRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
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
