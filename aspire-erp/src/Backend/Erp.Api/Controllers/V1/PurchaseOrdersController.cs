using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Purchase order endpoints (Task 4.1): create in Draft with the gapless PO-YYYY-NNNNN voucher and
/// the Draft -&gt; Submitted transition. No <c>[IdempotencyKeyRequired]</c> on either action: neither
/// writes StockLedgerEntry nor GLEntry, so Article VI.4 does not apply - workflow conflicts
/// (<c>invalid_status_transition</c>) surface as 409 instead.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public PurchaseOrdersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent purchase orders with their lines.</summary>
    /// <param name="companyId">Company that owns the orders.</param>
    /// <param name="limit">Maximum number of orders to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
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
                _errors.Text(PurchaseErrorCodes.CompanyNotFound),
                PurchaseErrorCodes.CompanyNotFound);
        }

        var orders = await _sender.SendAsync(new GetPurchaseOrdersQuery(companyId, page, pageSize), cancellationToken);
        return Ok(orders);
    }

    /// <summary>Creates one purchase order in Draft with its gapless PO voucher (no stock/GL impact).</summary>
    /// <param name="command">Order data (company, supplier, posting date, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                PurchaseErrorCodes.InvalidStatusTransition => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("PurchaseOrderConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PurchaseOrderRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var order = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = order.CompanyId }, order);
    }

    /// <summary>
    /// Updates a Draft purchase order. Replaces all line items.
    /// </summary>
    /// <param name="companyId">The company ID from the route.</param>
    /// <param name="id">The purchase order ID from the route.</param>
    /// <param name="command">The update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated purchase order details.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] UpdatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.PurchaseOrderId)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("BadRequest"),
                _errors.Text("route_id_mismatch"),
                "route_id_mismatch");
        }

        if (companyId != command.CompanyId)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("BadRequest"),
                _errors.Text("route_company_mismatch"),
                "route_company_mismatch");
        }

        var result = await _sender.SendAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;
        return error.Code switch
        {
            // Return the ObjectResult as-is: wrapping it again (NotFound(Problem(...))) would
            // serialize the inner result as a nested { value, formatters, ... } envelope instead
            // of the RFC 7807 body the client's ProblemDetails handling expects.
            PurchaseErrorCodes.PurchaseOrderNotFound => Problem(
                StatusCodes.Status404NotFound,
                _common.Text("PurchaseOrderNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            PurchaseErrorCodes.CompanyNotFound => Problem(
                StatusCodes.Status404NotFound,
                _common.Text("InvalidCompany"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            PurchaseErrorCodes.SupplierNotFound => Problem(
                StatusCodes.Status404NotFound,
                _common.Text("SupplierNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            PurchaseErrorCodes.InvalidStatusTransition => Problem(
                StatusCodes.Status409Conflict,
                // "Conflict", not the resource-specific title: PurchaseOrdersApiTests pins
                // this wire wording for the update path (same code, same title as before).
                _common.Text("Conflict"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                StatusCodes.Status409Conflict,
                _common.Text("ConcurrentUpdateConflict"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("BadRequest"),
                _errors.Text(error.Code, error.Message),
                error.Code),
        };
    }

    /// <summary>
    /// Advances one Draft order to Submitted (Task 4.1 workflow). Any state other than Draft is a 409
    /// (<c>invalid_status_transition</c>): the order conflicts with the requested state.
    /// </summary>
    /// <param name="id">Purchase order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidPurchaseOrder"),
                _errors.Text(PurchaseErrorCodes.PurchaseOrderNotFound),
                PurchaseErrorCodes.PurchaseOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitPurchaseOrderCommand(companyId, id), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                PurchaseErrorCodes.InvalidStatusTransition => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("PurchaseOrderConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("ConcurrentUpdateConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PurchaseOrderRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value);
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
