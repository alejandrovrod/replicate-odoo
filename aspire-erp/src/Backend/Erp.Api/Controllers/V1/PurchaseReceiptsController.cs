using Erp.Api.Common;
using Erp.Api.Filters;
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
/// Purchase receipt endpoints (Task 4.2): POST creates AND posts the receipt in one transaction -
/// +Kardex rows, Dr warehouse stock account / Cr Stock Received But Not Billed, gapless PR voucher
/// and the linked order's -&gt; Received transition. There is no draft state (decision D4), so the
/// POST carries the literal <c>[IdempotencyKeyRequired]</c> guard demanded by Article VI.4 because
/// it writes StockLedgerEntry AND GLEntry.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class PurchaseReceiptsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public PurchaseReceiptsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent purchase receipts with their lines.</summary>
    /// <param name="companyId">Company that owns the receipts.</param>
    /// <param name="limit">Maximum number of receipts to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseReceiptDto>), StatusCodes.Status200OK)]
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

        var receipts = await _sender.SendAsync(new GetPurchaseReceiptsQuery(companyId, page, pageSize), cancellationToken);
        return Ok(receipts);
    }

    /// <summary>
    /// Creates AND posts one purchase receipt in a single transaction: Kardex rows, balanced GL
    /// lines (Dr 1310 / Cr 2120), the gapless PR voucher and the order's Received transition.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400, a
    /// replayed key returns the stored body with HTTP 200, and reusing a key with a different payload
    /// is a 409. A Draft/Billed referenced order is a 409 (<c>invalid_status_transition</c>).
    /// </remarks>
    /// <param name="command">Receipt data (company, warehouse, optional order, posting date, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PurchaseReceiptPostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] PostPurchaseReceiptCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: workflow conflicts (an order that cannot receive), RowVersion races
            // (spec BY-06) and a frozen fiscal period (spec AC-04 - the request conflicts with
            // the state of the fiscal calendar) are 409, every other domain rejection is a bad
            // request carrying the stable machine code.
            return error.Code switch
            {
                // The status belongs to the linked purchase order, hence its title.
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
                AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("FiscalPeriodLocked"),
                    // Instance-valued detail (period dates): passthrough (Phase 2 convention).
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PurchaseReceiptRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = posting.Receipt.CompanyId }, posting);
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
