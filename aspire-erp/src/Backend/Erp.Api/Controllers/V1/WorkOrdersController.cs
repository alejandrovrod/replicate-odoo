using Erp.Api.Filters;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Manufacturing.Commands;
using Erp.Application.Features.Manufacturing.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Work order endpoints (Tasks 9.3/9.4/9.6): create in Draft with the gapless WO-YYYY-NNNNN voucher,
/// the Draft -&gt; Submitted transition, the MF-02 Stores -&gt; WIP transfer (Submitted -&gt;
/// InProcess), the MF-03 manufacture completion (InProcess -&gt; Completed) and the MF-05
/// cancellation (Submitted/InProcess -&gt; Cancelled, with a compensating WIP -&gt; Stores
/// transfer when materials were issued). Attributes follow
/// Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1) and
/// exhaustive status documentation (VI.3). The three ledger-posting mutations (transfer,
/// complete, cancel-with-reversal) carry the literal
/// <c>[IdempotencyKeyRequired]</c> guard (Article VI.4) - create, submit and the list read
/// write no StockLedgerEntry/GLEntry rows, so the purchase-order precedent (no filter) applies
/// to them.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class WorkOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public WorkOrdersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Creates one work order in Draft with its gapless WO voucher (no stock/GL impact).</summary>
    /// <param name="command">Order data (company, item, BOM, quantity, warehouses, planned dates).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                "Work Order Rejected",
                error.Message,
                error.Code);
        }

        var order = result.Value!;
        return CreatedAtAction(nameof(Submit), new { id = order.Id, companyId = order.CompanyId }, order);
    }

    /// <summary>
    /// Advances one Draft order to Submitted after verifying its BOM is active and default
    /// (Task 9.3 acceptance). Any state other than Draft is a 409
    /// (<c>invalid_status_transition</c>).
    /// </summary>
    /// <param name="id">Work order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
                "Invalid Work Order",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                ManufacturingErrorCodes.WorkOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitWorkOrderCommand(companyId, id), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                ManufacturingErrorCodes.WorkOrderNotFound or ManufacturingErrorCodes.BomNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Work Order Not Found",
                    error.Message,
                    error.Code),
                ManufacturingErrorCodes.InvalidStatusTransition or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    "Work Order Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Work Order Rejected",
                    error.Message,
                    error.Code),
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lists the company's work-order headers, newest first (Task 9.5 execution board reads).
    /// Read-only: no stock, no GL, no idempotency guard.
    /// </summary>
    /// <param name="companyId">Company that owns the orders.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                ManufacturingErrorCodes.WorkOrderNotFound);
        }

        var orders = await _sender.SendAsync(new GetWorkOrdersQuery(companyId), cancellationToken);
        return Ok(orders);
    }

    /// <summary>
    /// Transfers a Submitted order's components Stores -&gt; WIP (spec MF-02: Dr 1320 / Cr 1310 at
    /// FIFO value) and advances it to InProcess.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400,
    /// a replayed key returns the stored response verbatim, and reusing a key with a different
    /// payload is a 409. Duplicate completion replay safety (spec MF-04) rides this pipeline
    /// (proven live by WorkOrderManufacturingApiTests).
    /// </remarks>
    /// <param name="id">Work order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="postingDate">Accounting date of the transfer (defaults to today).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/transfer-to-wip")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(StockEntryPostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransferToWip(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] DateOnly? postingDate = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Work Order",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                ManufacturingErrorCodes.WorkOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new TransferMaterialsToWipCommand(companyId, id, postingDate), cancellationToken);

        if (!result.IsSuccess)
        {
            return ManufacturingProblem(result.Error!);
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Submit), new { id, companyId = posting.Entry.CompanyId }, posting);
    }

    /// <summary>
    /// Completes an InProcess order (spec MF-03): consumes WIP at FIFO cost, capitalizes operating
    /// cost (Dr 1330 TotalCost / Cr 1320 RawMaterialCost / Cr 5210 OperatingCost), receives the
    /// finished goods, and advances the order to Completed.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - same replay contract as
    /// the transfer route. Duplicate completion replay safety (spec MF-04) rides this pipeline
    /// (proven live by WorkOrderManufacturingApiTests).
    /// </remarks>
    /// <param name="id">Work order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="producedQuantity">Finished units received (within the authorized quantity).</param>
    /// <param name="postingDate">Accounting date of the manufacture (defaults to today).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/complete")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(StockEntryPostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] decimal producedQuantity,
        [FromQuery] DateOnly? postingDate = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Work Order",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                ManufacturingErrorCodes.WorkOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new CompleteManufactureCommand(companyId, id, producedQuantity, postingDate), cancellationToken);

        if (!result.IsSuccess)
        {
            return ManufacturingProblem(result.Error!);
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Submit), new { id, companyId = posting.Entry.CompanyId }, posting);
    }

    /// <summary>
    /// Cancels a Submitted or InProcess order (spec MF-05). An InProcess order (MF-02 transfer
    /// posted) additionally reverses its transfer with a compensating WIP -&gt; Stores voucher
    /// so WIP nets back to zero; a never-transferred order cancels as a pure status transition.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - the reversal posts
    /// StockLedgerEntry/GLEntry rows, so the same replay contract as the transfer and complete
    /// routes applies.
    /// </remarks>
    /// <param name="id">Work order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="postingDate">Accounting date of the reversal (defaults to today).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(WorkOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] DateOnly? postingDate = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Work Order",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                ManufacturingErrorCodes.WorkOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelWorkOrderCommand(companyId, id, postingDate), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                ManufacturingErrorCodes.WorkOrderNotFound or ManufacturingErrorCodes.BomNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Work Order Not Found",
                    error.Message,
                    error.Code),
                ManufacturingErrorCodes.InvalidStatusTransition or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    "Work Order Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Work Order Rejected",
                    error.Message,
                    error.Code),
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// RFC 7807 mapping for the manufacturing posting routes: missing masters are 404, state
    /// conflicts (bad transition, frozen period, concurrency race) are 409, every other domain
    /// failure - including the MF-06 insufficient-stock rejection - is a 400 carrying the stable
    /// machine code.
    /// </summary>
    private ObjectResult ManufacturingProblem(Error error) =>
        error.Code switch
        {
            ManufacturingErrorCodes.WorkOrderNotFound
                or ManufacturingErrorCodes.BomNotFound
                or ManufacturingErrorCodes.WorkstationNotFound
                or StockErrorCodes.CompanyNotFound
                or StockErrorCodes.WarehouseNotFound
                or StockErrorCodes.ItemNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Manufacturing Resource Not Found",
                    error.Message,
                    error.Code),
            ManufacturingErrorCodes.InvalidStatusTransition
                or ConcurrencyErrorCodes.ConcurrencyConflict
                or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    "Manufacturing Conflict",
                    error.Message,
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "Manufacturing Rejected",
                error.Message,
                error.Code),
        };

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
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
