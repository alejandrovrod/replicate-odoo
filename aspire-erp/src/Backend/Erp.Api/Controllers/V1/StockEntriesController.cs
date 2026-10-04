using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Stock.Commands;
using Erp.Application.Features.Stock.Queries;
using Erp.Api.Filters;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Stock voucher endpoints (Task 3.2 posting + Task 3.3 negative-stock rule). Attributes follow
/// Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1), content
/// negotiation (VI.2) and exhaustive status documentation (VI.3) - and the POST carries the
/// literal <c>[IdempotencyKeyRequired]</c> guard demanded by Article VI.4 because it posts to
/// StockLedgerEntry AND GLEntry.
/// </summary>
/// <remarks>
/// There is no draft lifecycle (decision D4): POST creates AND posts the voucher atomically, so
/// the 201 body carries the voucher, its Kardex rows and its balanced General Ledger lines.
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class StockEntriesController : ControllerBase
{
    private readonly ISender _sender;

    public StockEntriesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's most recent stock vouchers with their lines.</summary>
    /// <param name="companyId">Company that owns the vouchers.</param>
    /// <param name="limit">Maximum number of vouchers to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StockEntryDto>), StatusCodes.Status200OK)]
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
                StockErrorCodes.CompanyNotFound);
        }

        var entries = await _sender.SendAsync(new GetStockEntriesQuery(companyId, limit), cancellationToken);
        return Ok(entries);
    }

    /// <summary>
    /// Creates AND posts one stock voucher (receipt / issue / transfer) in a single transaction:
    /// FIFO valuation, Kardex rows, balanced General Ledger lines and a gapless voucher number.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400,
    /// a replayed key returns the stored response verbatim, and reusing a key with a different
    /// payload is a 409.
    /// </remarks>
    /// <param name="command">Voucher data (company, type, warehouse(s), posting date, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(StockEntryPostingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStockEntryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: duplicates conflict (and so does a frozen fiscal period, spec AC-04 -
            // 409 keeps every state conflict in one status), every other domain failure -
            // including the Task 3.3 negative-stock rejection - is a bad request carrying the
            // stable machine code.
            return error.Code switch
            {
                StockErrorCodes.DuplicateItemCode
                    or StockErrorCodes.DuplicateWarehouseCode => Problem(
                        StatusCodes.Status409Conflict,
                        "Duplicate Stock Master",
                        error.Message,
                        error.Code),
                AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    "Fiscal Period Locked",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Stock Entry Rejected",
                    error.Message,
                    error.Code),
            };
        }

        var posting = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = posting.Entry.CompanyId }, posting);
    }

    /// <summary>
    /// Cancels a stock voucher (spec AC-07). Cancellation is append-only: it flags the voucher as
    /// cancelled and appends compensating (negative) rows to the Kardex and General Ledger.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new CancelStockEntryCommand(companyId, id), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                StockErrorCodes.VoucherNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Stock Entry Not Found",
                    error.Message,
                    error.Code),
                StockErrorCodes.InvalidStatusTransition or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    "Conflict",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Cancellation Rejected",
                    error.Message,
                    error.Code),
            };
        }

        return Ok();
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
