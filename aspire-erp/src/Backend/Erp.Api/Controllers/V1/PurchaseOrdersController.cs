using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Purchase order endpoints (Task 4.1): create in Draft with the gapless PO-YYYY-NNNNN voucher and
/// the Draft -&gt; Ordered transition. No <c>[IdempotencyKeyRequired]</c> on either action: neither
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

    public PurchaseOrdersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's most recent purchase orders with their lines.</summary>
    /// <param name="companyId">Company that owns the orders.</param>
    /// <param name="limit">Maximum number of orders to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                PurchaseErrorCodes.CompanyNotFound);
        }

        var orders = await _sender.SendAsync(new GetPurchaseOrdersQuery(companyId, limit), cancellationToken);
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
                    "Purchase Order Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Purchase Order Rejected",
                    error.Message,
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
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "The ID in the route must match the ID in the payload.");
        }

        if (companyId != command.CompanyId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "The Company ID in the route must match the payload.");
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
            PurchaseErrorCodes.PurchaseOrderNotFound or PurchaseErrorCodes.CompanyNotFound or PurchaseErrorCodes.SupplierNotFound => Problem(
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                error.Message,
                error.Code),
            PurchaseErrorCodes.InvalidStatusTransition or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                StatusCodes.Status409Conflict,
                "Conflict",
                error.Message,
                error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                error.Message,
                error.Code),
        };
    }

    /// <summary>
    /// Advances one Draft order to Ordered (Task 4.1 workflow). Any state other than Draft is a 409
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
                "Invalid Purchase Order",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
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
                    "Purchase Order Conflict",
                    error.Message,
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    "Concurrent Update Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Purchase Order Rejected",
                    error.Message,
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
