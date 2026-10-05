using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.GeneralLedger.Commands;
using Erp.Application.Features.GeneralLedger.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Journal Entry endpoints (tasks.md 2.3/2.4): the two-step manual-voucher workflow
/// create (Draft) -&gt; submit (ledger append) -&gt; cancel (compensating reversal), plus the list
/// and single reads the tests and the future UI need (tasks 2.5/2.6 read vouchers through these).
/// Attributes follow Constitution Article VI: explicit route + versioning + TenantMember policy
/// (VI.1), JSON content negotiation (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// <para>The two transition actions DO post to GLEntry, so Article VI.4 demands the 
/// <c>[IdempotencyKeyRequired]</c> filter to prevent duplicate submissions (AC-06 / 
/// "Idempotent Submission Guard").</para>
/// <para><b>Consumes placement.</b> Mirrors PurchaseOrdersController: <c>[Consumes("application/json")]</c>
/// only on the action that always receives a body (create); the transition bodies are OPTIONAL
/// (an empty body is legal), so constraining their content type would 415 a client that POSTs
/// nothing.</para>
/// <para><b>Two route templates (constitution VI.1 vs tasks.md 2.4 literal path).</b>
/// Constitution Article VI.1 mandates <c>[Route("api/v1/[controller]")]</c>, which yields
/// <c>/api/v1/JournalEntries</c>; tasks.md 2.4 asks the endpoints to be exposed at
/// <c>/api/v1/journal-entries</c> (kebab-case). Route matching is case-insensitive but NOT
/// hyphen-insensitive, so the literal tasks.md path would 404 on <c>[controller]</c> alone -
/// the controller therefore declares BOTH templates (attribute routing allows multiple
/// <c>[Route]</c> attributes) and neither authority is violated. No path matches both templates,
/// so request matching never becomes ambiguous.</para>
/// <para><b>Status mapping</b> (the established pipeline):
/// <c>invalid_status_transition</c>, <c>fiscal_period_locked</c> and
/// <c>concurrency_conflict</c> are STATE conflicts -&gt; 409; <c>journal_entry_not_found</c> -&gt; 404;
/// everything else (<c>double_entry_imbalance</c>, <c>posting_to_group_account_prohibited</c>,
/// <c>invalid_amount</c>, <c>no_lines</c>, ...) describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/journal-entries")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class JournalEntriesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public JournalEntriesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent journal entries with their lines.</summary>
    /// <param name="companyId">Company that owns the vouchers.</param>
    /// <param name="limit">Maximum number of vouchers to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JournalEntryDto>), StatusCodes.Status200OK)]
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
                _common.Text("InvalidCompany"),
                _errors.Text(JournalErrorCodes.CompanyNotFound),
                JournalErrorCodes.CompanyNotFound);
        }

        var entries = await _sender.SendAsync(
            new GetJournalEntriesQuery(companyId, limit), cancellationToken);
        return Ok(entries);
    }

    /// <summary>Returns one journal entry (header + lines) by id.</summary>
    /// <param name="id">Journal entry id.</param>
    /// <param name="companyId">Company that owns the voucher.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidJournalEntry"),
                _errors.Text(JournalErrorCodes.JournalEntryNotFound),
                JournalErrorCodes.JournalEntryNotFound);
        }

        var entry = await _sender.SendAsync(
            new GetJournalEntryQuery(companyId, id), cancellationToken);

        return entry is null
            ? NotFoundProblem(id)
            : Ok(entry);
    }

    /// <summary>
    /// Creates ONE journal entry in Draft with its gapless JV-YYYY-NNNNN voucher - NO ledger rows
    /// yet (spec AC-02/AC-03 start from such a draft).
    /// </summary>
    /// <param name="command">Voucher data (company, posting date, type, remark, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                JournalErrorCodes.JournalEntryNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("JournalEntryNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("JournalEntryRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var entry = result.Value!;
        return CreatedAtAction(
            nameof(GetById),
            new { id = entry.Id, companyId = entry.CompanyId },
            entry);
    }

    /// <summary>
    /// Submits one Draft journal entry: runs the plan §3 validation chain (balance, freeze, group
    /// account) and appends N balanced GLEntry rows + the Submitted transition in ONE transaction
    /// (spec AC-01/AC-02/AC-03/AC-04; tasks.md 2.4 acceptance "updates ledger balances atomically").
    /// </summary>
    /// <remarks>
    /// The body is OPTIONAL: send <c>{ "rowVersion": "..." }</c> for a compare-and-swap transition
    /// (stale token -&gt; 409 <c>concurrency_conflict</c>), or omit it entirely.
    /// </remarks>
    /// <param name="id">Journal entry id.</param>
    /// <param name="companyId">Company that owns the voucher.</param>
    /// <param name="request">Optional optimistic concurrency token.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JournalEntryStatusRequest? request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidJournalEntry"),
                _errors.Text(JournalErrorCodes.JournalEntryNotFound),
                JournalErrorCodes.JournalEntryNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitJournalEntryCommand(companyId, id, request?.RowVersion), cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// Cancels one Submitted journal entry: appends the compensating reversal rows (Debit/Credit
    /// swapped, original PostingDate, <c>IsCancelled = true</c>) and moves the header to
    /// Cancelled, atomically (spec AC-07 / Constitution III.3).
    /// </summary>
    /// <remarks>The body is OPTIONAL - same contract as <see cref="Submit"/>.</remarks>
    /// <param name="id">Journal entry id.</param>
    /// <param name="companyId">Company that owns the voucher.</param>
    /// <param name="request">Optional optimistic concurrency token.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(JournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JournalEntryStatusRequest? request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidJournalEntry"),
                _errors.Text(JournalErrorCodes.JournalEntryNotFound),
                JournalErrorCodes.JournalEntryNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelJournalEntryCommand(companyId, id, request?.RowVersion), cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Maps one transition outcome to RFC 7807 (see the class remarks for the matrix).</summary>
    private ObjectResult ToActionResult(Result<JournalEntryDto> result)
    {
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                JournalErrorCodes.JournalEntryNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("JournalEntryNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                JournalErrorCodes.InvalidStatusTransition
                    or AccountingErrorCodes.FiscalPeriodLocked
                    or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    error.Code switch
                    {
                        AccountingErrorCodes.FiscalPeriodLocked => _common.Text("FiscalPeriodLocked"),
                        ConcurrencyErrorCodes.ConcurrencyConflict => _common.Text("ConcurrentUpdateConflict"),
                        _ => _common.Text("JournalEntryConflict"),
                    },
                    error.Code switch
                    {
                        // Instance-valued detail (period dates, pinned by JournalEntriesApiTests
                        // and FiscalPeriodLockApiTests): passes through (Phase 2 convention).
                        AccountingErrorCodes.FiscalPeriodLocked => error.Message,
                        _ => _errors.Text(error.Code, error.Message),
                    },
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("JournalEntryRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value);
    }

    /// <summary>RFC 7807 404 for a voucher that does not exist (or is not visible) in this tenant.</summary>
    private ObjectResult NotFoundProblem(Guid id) =>
        Problem(
            StatusCodes.Status404NotFound,
            _common.Text("JournalEntryNotFound"),
            _errors.Text(JournalErrorCodes.JournalEntryNotFound),
            JournalErrorCodes.JournalEntryNotFound);

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
