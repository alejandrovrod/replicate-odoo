using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Application.Features.Payments.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Supplier master endpoints (Task 4.1: codes unique per TENANT). No
/// <c>[IdempotencyKeyRequired]</c> here: Article VI.4 scopes that guard to ledger-posting mutations
/// and creating a supplier touches only the Supplier table.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ISupplierRepository _suppliers;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SuppliersController(
        ISender sender,
        ISupplierRepository suppliers,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _suppliers = suppliers;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the tenant's most recent suppliers.</summary>
    /// <param name="limit">Maximum number of suppliers to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:supplier:read")]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _sender.SendAsync(new GetSuppliersQuery(page, pageSize), cancellationToken);
        return Ok(suppliers);
    }

    /// <summary>Creates one supplier (unique code per tenant - duplicates are a 409).</summary>
    /// <param name="command">Supplier data (code, name, active flag).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:supplier:write")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate code conflicts with the existing master, anything else is a
            // field/lookup rejection carrying the stable machine code.
            return error.Code switch
            {
                PurchaseErrorCodes.DuplicateSupplierCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateSupplierCode"),
                    // Instance-valued detail (the colliding code): passes through
                    // untranslated, the title still localizes (Phase 2 convention).
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SupplierRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return CreatedAtAction(nameof(Get), new { }, result.Value);
    }

    /// <summary>Updates an existing supplier. 200 with the updated supplier.</summary>
    /// <param name="id">Supplier ID.</param>
    /// <param name="command">Updated supplier data including RowVersion.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "permission:supplier:write")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidSupplierId"),
                _errors.Text("supplier_not_found"),
                "supplier_not_found");
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            return error.Code switch
            {
                "supplier_not_found" => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("SupplierNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict or PurchaseErrorCodes.DuplicateSupplierCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateSupplierCode"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SupplierRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Disables a supplier (sets IsActive = false).</summary>
    [HttpPut("{id}/disable")]
    [Authorize(Policy = "permission:supplier:write")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, false, cancellationToken);

    /// <summary>Enables a supplier (sets IsActive = true).</summary>
    [HttpPut("{id}/enable")]
    [Authorize(Policy = "permission:supplier:write")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("SupplierNotFound"),
                _errors.Text("supplier_not_found"),
                "supplier_not_found");
        }

        supplier.IsActive = isActive;
        await _suppliers.UpdateAsync(supplier, cancellationToken);
        return Ok(SupplierDto.From(supplier));
    }

    /// <summary>
    /// Returns the supplier's open payables for the payment allocation grid (spec R-12):
    /// bills with OutstandingAmount &gt; 0 and Unpaid/PartiallyPaid status, oldest due first.
    /// </summary>
    /// <param name="id">Supplier ID.</param>
    /// <param name="companyId">Company that owns the bills (suppliers themselves are tenant-wide).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id}/outstanding-invoices")]
    [Authorize(Policy = "permission:supplier:read")]
    [ProducesResponseType(typeof(IReadOnlyList<OutstandingInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOutstandingInvoices(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidSupplierId"),
                _errors.Text("supplier_not_found"),
                "supplier_not_found");
        }

        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("SupplierNotFound"),
                _errors.Text("supplier_not_found"),
                "supplier_not_found");
        }

        var bills = await _sender.SendAsync(
            new GetOutstandingPurchaseInvoicesQuery(companyId, id), cancellationToken);
        return Ok(bills);
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
