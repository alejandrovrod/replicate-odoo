using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Bank transaction workbench endpoints (Block B, tasks 6.3/6.4): the staging-line list, the
/// heuristic rule run, and the reconcile / un-reconcile transitions. Attributes follow
/// Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1), JSON
/// content negotiation (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// <b>Status mapping</b> (the established pipeline): unknown ids
/// (<c>bank_transaction_not_found</c>, <c>payment_entry_not_found</c>,
/// <c>gl_voucher_not_found</c>) -&gt; 404; state conflicts
/// (<c>invalid_status_transition</c>, <c>concurrency_conflict</c>) -&gt; 409; every other
/// domain rejection (amount mismatch, over-allocation, bad line shape) -&gt; 400.
/// </remarks>
[ApiController]
[Route("api/v1/bank-transactions")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class BankTransactionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BankTransactionsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Rule-run request: every unreconciled line of the scope is evaluated.</summary>
    public sealed record RunRulesRequest(Guid CompanyId, Guid? BankAccountId = null);

    /// <summary>One allocation slice of a reconcile request.</summary>
    public sealed record ReconciliationLineRequest(
        Guid? PaymentEntryId,
        Guid? GlVoucherId,
        decimal Amount);

    /// <summary>Reconcile request: slices must sum to exactly |Deposit - Withdrawal| (BN-04).</summary>
    public sealed record ReconcileRequest(
        Guid CompanyId,
        List<ReconciliationLineRequest> Lines,
        byte[]? RowVersion = null);

    /// <summary>Un-reconcile request (scenario BN-06).</summary>
    public sealed record UnreconcileRequest(Guid CompanyId, byte[]? RowVersion = null);

    /// <summary>
    /// Quick-voucher request (task 6.5, scenario BN-04): posts a balanced SUBMITTED journal
    /// voucher from the line and reconciles it atomically. <c>Amount</c> defaults to
    /// |Deposit - Withdrawal|; when given it must equal that value exactly.
    /// </summary>
    public sealed record QuickVoucherRequest(
        Guid CompanyId,
        string ExpenseAccountCode,
        decimal? Amount = null,
        string? Memo = null,
        byte[]? RowVersion = null);

    /// <summary>Returns the company's staging lines (optional account / status filter).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BankTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? bankAccountId = null,
        [FromQuery] BankTransactionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(BankingErrorCodes.CompanyNotFound),
                BankingErrorCodes.CompanyNotFound);
        }

        var transactions = await _sender.SendAsync(
            new GetBankTransactionsQuery(companyId, bankAccountId, status), cancellationToken);
        return Ok(transactions);
    }

    /// <summary>
    /// Runs the heuristic rules engine over the scope's unreconciled lines (task 6.3,
    /// scenario BN-02): first matching rule wins per line -&gt; Matched + suggestions.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4): the run mutates
    /// staging lines (Unreconciled -&gt; Matched).
    /// </remarks>
    [HttpPost("run-rules")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(RuleMatchSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunRules(
        [FromBody] RunRulesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new ApplyMatchingRulesCommand(request.CompanyId, request.BankAccountId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("ConcurrentUpdateConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("RuleRunRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>
    /// Reconciles one staging line against payment vouchers and/or posted GL vouchers
    /// (task 6.4, BN-03/BN-04): links are persisted, Status becomes Reconciled and
    /// ClearanceDate is stamped on both sides - the difference drops to $0.00.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4).
    /// </remarks>
    [HttpPost("{id:guid}/reconcile")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(ReconciliationSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reconcile(
        [FromRoute] Guid id,
        [FromBody] ReconcileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new ReconcileBankTransactionCommand(
                request.CompanyId,
                id,
                request.Lines
                    .Select(l => new ReconciliationLine(l.PaymentEntryId, l.GlVoucherId, l.Amount))
                    .ToList(),
                request.RowVersion),
            cancellationToken);

        return ToReconcileActionResult(result);
    }

    /// <summary>
    /// Reverts a reconciliation (scenario BN-06): links deleted, both sides back to
    /// Unreconciled with NULL clearance and zero allocation.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4).
    /// </remarks>
    [HttpPost("{id:guid}/unreconcile")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unreconcile(
        [FromRoute] Guid id,
        [FromBody] UnreconcileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new UnreconcileBankTransactionCommand(request.CompanyId, id, request.RowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                BankingErrorCodes.BankTransactionNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("BankTransactionNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                BankingErrorCodes.InvalidStatusTransition
                    or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("Conflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("UnreconcileRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok();
    }

    /// <summary>
    /// Creates a balanced SUBMITTED journal voucher from one staging line and reconciles the
    /// line against it, atomically (task 6.5, scenario BN-04: a $15 bank fee becomes
    /// Dr expense / Cr bank with the difference at $0.00).
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4).
    /// </remarks>
    [HttpPost("{id:guid}/quick-voucher")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuickVoucher(
        [FromRoute] Guid id,
        [FromBody] QuickVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(
            new CreateVoucherFromBankTransactionCommand(
                request.CompanyId,
                id,
                request.ExpenseAccountCode,
                request.Amount,
                request.Memo,
                request.RowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                BankingErrorCodes.BankTransactionNotFound
                    or BankingErrorCodes.BankAccountNotFound
                    or BankingErrorCodes.CompanyNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("QuickVoucherCounterpartNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                BankingErrorCodes.InvalidStatusTransition
                    or ConcurrencyErrorCodes.ConcurrencyConflict
                    or AccountingErrorCodes.FiscalPeriodLocked => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("Conflict"),
                    error.Code switch
                    {
                        // Instance-valued detail (period dates) passes through (Phase 2 convention).
                        AccountingErrorCodes.FiscalPeriodLocked => error.Message,
                        _ => _errors.Text(error.Code, error.Message),
                    },
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("QuickVoucherRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var entry = result.Value!;
        return CreatedAtAction(nameof(QuickVoucher), new { id }, entry);
    }

    private ObjectResult ToReconcileActionResult(Result<ReconciliationSummary> result)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result.Value!) { StatusCode = StatusCodes.Status200OK };
        }

        var error = result.Error!;
        return error.Code switch
        {
            BankingErrorCodes.BankTransactionNotFound
                or BankingErrorCodes.PaymentEntryNotFound
                or BankingErrorCodes.GlVoucherNotFound => Problem(
                StatusCodes.Status404NotFound,
                _common.Text("ReconciliationCounterpartNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            BankingErrorCodes.InvalidStatusTransition
                or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                StatusCodes.Status409Conflict,
                _common.Text("Conflict"),
                _errors.Text(error.Code, error.Message),
                error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("ReconciliationRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code),
        };
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
