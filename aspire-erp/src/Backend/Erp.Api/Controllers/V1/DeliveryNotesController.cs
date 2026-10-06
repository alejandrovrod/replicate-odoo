using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Erp.Application.Features.Selling.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Delivery note endpoints (Task 5.2b / Amendment A1): POST creates AND posts the note in one
/// transaction - FIFO valuation, -Kardex rows, Dr Cost of Goods Sold / Cr warehouse stock account,
/// the gapless DN voucher and the linked order's delivery update. There is no draft state, so the
/// POST carries the literal <c>[IdempotencyKeyRequired]</c> guard demanded by Article VI.4 because
/// it writes StockLedgerEntry AND GLEntry.
/// </summary>
/// <remarks>
/// <para><b>Two route templates (constitution VI.1 vs the kebab-case document path).</b>
/// Constitution Article VI.1 mandates <c>[Route("api/v1/[controller]")]</c>; the selling documents
/// are addressed at <c>/api/v1/delivery-notes</c>. Route matching is case-insensitive but NOT
/// hyphen-insensitive, so the controller declares BOTH templates (the JournalEntriesController
/// precedent) and neither authority is violated. No path matches both templates.</para>
/// <para><b>Status mapping</b> (the established pipeline):
/// <c>sales_order_not_deliverable</c>, <c>fiscal_period_locked</c> and
/// <c>concurrency_conflict</c> are STATE conflicts -&gt; 409; <c>delivery_note_not_found</c> and
/// <c>sales_order_not_found</c> -&gt; 404; everything else (<c>overdelivery_not_allowed</c>,
/// <c>insufficient_stock</c>, field codes) describes a bad REQUEST -&gt; 400. Configuration faults
/// deliberately bubble up as 500.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/delivery-notes")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class DeliveryNotesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public DeliveryNotesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent delivery notes with their lines.</summary>
    /// <param name="companyId">Company that owns the notes.</param>
    /// <param name="limit">Maximum number of notes to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DeliveryNoteDto>), StatusCodes.Status200OK)]
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
                _errors.Text(SellingErrorCodes.CompanyRequired),
                SellingErrorCodes.CompanyRequired);
        }

        var notes = await _sender.SendAsync(new GetDeliveryNotesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(notes);
    }

    /// <summary>Returns one delivery note (header + lines) by id.</summary>
    /// <param name="id">Delivery note id.</param>
    /// <param name="companyId">Company that owns the note.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeliveryNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidDeliveryNote"),
                _errors.Text(SellingErrorCodes.DeliveryNoteNotFound),
                SellingErrorCodes.DeliveryNoteNotFound);
        }

        var note = await _sender.SendAsync(
            new GetDeliveryNoteByIdQuery(companyId, id), cancellationToken);

        return note is null
            ? NotFoundProblem(id)
            : Ok(note);
    }

    /// <summary>
    /// Creates AND posts one delivery note in a single transaction: FIFO valuation, -Kardex rows,
    /// balanced GL lines (Dr 5210 Cost of Goods Sold / Cr warehouse stock account), the gapless DN
    /// voucher and the order's DeliveredQuantity/DeliveredPercentage/status update.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400, a
    /// replayed key returns the stored response verbatim, and reusing a key with a different payload
    /// is a 409. An order that cannot ship (Draft/Completed/Cancelled) is a 409
    /// (<c>sales_order_not_deliverable</c>); shipping more than the order owes is a 400
    /// (<c>overdelivery_not_allowed</c>, spec SL-04).
    /// </remarks>
    /// <param name="command">Delivery data (company, order, warehouse, posting date, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(DeliveryNotePostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] PostDeliveryNoteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: workflow conflicts (an order that cannot receive deliveries), RowVersion
            // races and a frozen fiscal period are 409; a missing order is 404; every other domain
            // rejection is a bad request carrying the stable machine code.
            return error.Code switch
            {
                SellingErrorCodes.SalesOrderNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("SalesOrderNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                SellingErrorCodes.SalesOrderNotDeliverable => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("SalesOrderConflict"),
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
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("DeliveryNoteRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = posting.DeliveryNote.CompanyId }, posting);
    }

    /// <summary>RFC 7807 404 for a note that does not exist (or is not visible) in this tenant.</summary>
    private ObjectResult NotFoundProblem(Guid id) =>
        Problem(
            StatusCodes.Status404NotFound,
            _common.Text("DeliveryNoteNotFound"),
            _errors.Text(SellingErrorCodes.DeliveryNoteNotFound),
            SellingErrorCodes.DeliveryNoteNotFound);

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
