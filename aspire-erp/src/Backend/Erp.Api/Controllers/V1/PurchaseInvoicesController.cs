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
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Purchase invoice endpoints (Task 4.3 / spec BY-01): POST creates AND posts the vendor bill in
/// one transaction - Dr Stock Received But Not Billed at receipt value + Dr Input Tax Recoverable +
/// Dr/Cr price difference / Cr Accounts Payable, gapless PINV voucher and the order's Billed
/// transition. Several invoices may bill the SAME receipt, each one progressively against the
/// cumulative quantity already billed (Task 4.4); billing past what the receipt still has available
/// is rejected with 400 <c>overbilling_not_allowed</c>. The POST carries the literal
/// <c>[IdempotencyKeyRequired]</c> guard demanded by Article VI.4 because it writes GLEntry.
/// </summary>
/// <remarks>
/// <para><b>Cancellation (Task 4.6 / spec BY-05).</b> <c>POST {id}/cancel</c> appends the
/// compensating reversal rows (Debit/Credit swapped, original PostingDate,
/// <c>IsCancelled = true</c>) and moves the bill Unpaid/PartiallyPaid -&gt; Cancelled, atomically;
/// it is guarded by the same idempotency filter and answers <see cref="PurchaseInvoiceDto"/>.</para>
/// <para><b>Status mapping.</b> <c>purchase_invoice_not_found</c> -&gt; 404;
/// <c>invoice_already_cancelled</c>, <c>invalid_status_transition</c>, <c>invoice_not_posted</c>,
/// <c>concurrency_conflict</c> and <c>fiscal_period_locked</c> are STATE conflicts -&gt; 409;
/// everything else is a bad REQUEST -&gt; 400.</para>
/// <para><b>Consumes placement.</b> <c>[Consumes("application/json")]</c> only on the action that
/// always receives a body (create); the cancel body is OPTIONAL (an empty body is legal), so
/// constraining its content type would 415 a client that POSTs nothing - mirrors
/// PurchaseOrdersController and JournalEntriesController.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class PurchaseInvoicesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public PurchaseInvoicesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent purchase invoices with their lines.</summary>
    /// <param name="companyId">Company that owns the invoices.</param>
    /// <param name="limit">Maximum number of invoices to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseInvoiceDto>), StatusCodes.Status200OK)]
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

        var invoices = await _sender.SendAsync(new GetPurchaseInvoicesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(invoices);
    }

    /// <summary>
    /// Creates AND posts one purchase invoice in a single transaction: interim liability clearance,
    /// input tax and price difference against a balanced Accounts Payable credit (spec BY-01).
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400, a
    /// replayed key answers 200 OK with the stored body verbatim (spec BY-04), and reusing a key
    /// with a different payload is a 409. Several invoices may bill the SAME receipt: each bill is
    /// checked against the cumulative quantity already billed on its receipt lines, and one that
    /// would exceed what was received is a 400 <c>overbilling_not_allowed</c> (spec BY-03 / BY-06,
    /// Task 4.4), which books no ledger row at all. Coverage and identity mismatches of the
    /// three-way match stay 400 as well.
    /// </remarks>
    /// <param name="command">Invoice data (company, receipt, posting date, tax, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PurchaseInvoicePostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] PostPurchaseInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate bill, a stale order status, a RowVersion race (spec BY-06) or a
            // frozen fiscal period (spec AC-04) are STATE conflicts -> 409; an overbilling breach
            // (spec BY-03 / Task 4.4) and every other domain rejection (three-way match included)
            // is a bad request.
            return error.Code switch
            {
                PurchaseErrorCodes.InvoiceAlreadyExists => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicatePurchaseInvoice"),
                    // Instance-valued detail (the bill number): passthrough (Phase 2 convention).
                    error.Message,
                    error.Code),
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
                PurchaseErrorCodes.OverbillingNotAllowed => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("OverbillingNotAllowed"),
                    // Instance-valued detail (quantities, pinned by
                    // PurchaseInvoiceOverbillingApiTests): passthrough.
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PurchaseInvoiceRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = posting.Invoice.CompanyId }, posting);
    }

    /// <summary>
    /// Cancels one posted purchase invoice: appends the compensating reversal rows (Debit/Credit
    /// swapped, original PostingDate, <c>IsCancelled = true</c>) and moves the bill
    /// Unpaid/PartiallyPaid -&gt; Cancelled, atomically, leaving the voucher netted to 0.0000 and
    /// Accounts Payable restored (Task 4.6 / spec BY-05 / Constitution III.3).
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4) and answers 200 on a replay
    /// (spec BY-04). The body is OPTIONAL - only an optimistic concurrency token, exposed as
    /// <c>rowVersion</c> on the invoice DTO (GET/POST responses).
    /// </remarks>
    /// <param name="id">Purchase invoice id.</param>
    /// <param name="companyId">Company that owns the invoice.</param>
    /// <param name="request">Optional optimistic concurrency token.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PurchaseInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PurchaseInvoiceStatusRequest? request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidPurchaseInvoice"),
                _errors.Text(PurchaseErrorCodes.InvoiceNotFound),
                PurchaseErrorCodes.InvoiceNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelPurchaseInvoiceCommand(companyId, id, request?.RowVersion), cancellationToken);

        return ToCancelActionResult(result);
    }

    /// <summary>Maps a cancellation outcome to RFC 7807 (see the class remarks for the matrix).</summary>
    private ObjectResult ToCancelActionResult(Result<PurchaseInvoiceDto> result)
    {
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                PurchaseErrorCodes.InvoiceNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("PurchaseInvoiceNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                PurchaseErrorCodes.InvoiceAlreadyCancelled
                    or PurchaseErrorCodes.InvalidStatusTransition
                    or PurchaseErrorCodes.InvoiceNotPosted
                    or ConcurrencyErrorCodes.ConcurrencyConflict
                    or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    error.Code switch
                    {
                        ConcurrencyErrorCodes.ConcurrencyConflict => _common.Text("ConcurrentUpdateConflict"),
                        AccountingErrorCodes.FiscalPeriodLocked => _common.Text("FiscalPeriodLocked"),
                        _ => _common.Text("PurchaseInvoiceConflict"),
                    },
                    error.Code switch
                    {
                        // Instance-valued details (period dates) pass through (Phase 2 convention).
                        AccountingErrorCodes.FiscalPeriodLocked => error.Message,
                        _ => _errors.Text(error.Code, error.Message),
                    },
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("PurchaseInvoiceRejected"),
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
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
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
