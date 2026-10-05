using Erp.Api.Filters;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.HrPayroll.Commands;
using Erp.Application.Features.HrPayroll.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>One attendance input of a submit run (no attendance/leave module - arrives as run input).</summary>
public sealed record PaymentDayOverrideRequest(Guid EmployeeId, int PaymentDays, int AbsentDays);

/// <summary>Submit-run body: company, period, accrual posting date and optional payment-day overrides.</summary>
public sealed record SubmitPayrollRunRequest(
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PostingDate,
    IReadOnlyList<PaymentDayOverrideRequest>? PaymentDayOverrides);

/// <summary>
/// Payroll batch endpoints (Tasks 12.3-12.4): the atomic submit run (slips + accrual voucher),
/// the bank disbursement pair and the accrual-reversing cancel, plus the list/detail reads
/// Block C UI/tests build on. Attributes follow Constitution Article VI: explicit route +
/// versioning + TenantMember policy (VI.1) and exhaustive status documentation (VI.3). The
/// three GL-posting mutations carry the literal <c>[IdempotencyKeyRequired]</c> guard
/// (Article VI.4); the reads write no GLEntry rows, so no filter applies to them.
/// </summary>
/// <remarks>
/// SCOPE (documented): there are NO employee/component/structure master endpoints here.
/// Masters enter via SQL seeds like BOMs (out of scope) - this controller owns the batch
/// lifecycle only. There is no declining-balance support, no attendance/leave module and no
/// banking PaymentEntry voucher on disbursement (GL pair + Paid status only).
/// </remarks>
[ApiController]
[Route("api/v1/payroll-runs")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class PayrollEntriesController : ControllerBase
{
    private readonly ISender _sender;

    public PayrollEntriesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Runs one monthly payroll batch: generates one salary slip per eligible employee, posts
    /// the per-component accrual voucher (Dr earnings / Cr deductions / Cr 2150 payable) and
    /// marks the entry Submitted - atomically.
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4). A missing header is a 400,
    /// a replayed key returns the stored response verbatim, and reusing a key with a different
    /// payload is a 409 (spec HR-04).
    /// </remarks>
    /// <param name="request">Company, period, posting date and optional payment-day overrides.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("submit")]
    [IdempotencyKeyRequired]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PayrollSubmitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitPayrollRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId must be a non-empty GUID.",
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitPayrollRunCommand(
                request.CompanyId,
                request.StartDate,
                request.EndDate,
                request.PostingDate,
                request.PaymentDayOverrides
                    ?.Select(o => new PaymentDayOverride(o.EmployeeId, o.PaymentDays, o.AbsentDays))
                    .ToList()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return PayrollProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Disburses one submitted run (spec HR-02 Phase 2): posts the Dr 2150 / Cr bank pair and
    /// marks the entry Paid.
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - same replay contract as submit.</remarks>
    /// <param name="id">Payroll entry id.</param>
    /// <param name="companyId">Company that owns the entry.</param>
    /// <param name="bankAccountId">Bank account paying the net salaries.</param>
    /// <param name="postingDate">Accounting date of the disbursement.</param>
    /// <param name="rowVersion">Optional optimistic token (base64) - a stale token fails fast.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/disburse")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PayrollEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disburse(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] Guid bankAccountId,
        [FromQuery] DateOnly postingDate,
        [FromQuery] byte[]? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || bankAccountId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Payroll Disbursement",
                "The route id, companyId and bankAccountId must all be non-empty GUIDs.",
                HrPayrollErrorCodes.PayrollEntryNotFound);
        }

        var result = await _sender.SendAsync(
            new DisbursePayrollCommand(companyId, id, bankAccountId, postingDate, rowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return PayrollProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cancels one submitted run (spec HR-05): mirrors the accrual voucher, marks the slips
    /// Cancelled and the entry Cancelled. Paid runs are never reversed here (409).
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - the mirror posts GLEntry rows.</remarks>
    /// <param name="id">Payroll entry id.</param>
    /// <param name="companyId">Company that owns the entry.</param>
    /// <param name="postingDate">Accounting date of the reversal (defaults to today).</param>
    /// <param name="rowVersion">Optional optimistic token (base64) - a stale token fails fast.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PayrollEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] DateOnly? postingDate = null,
        [FromQuery] byte[]? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Payroll Entry",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                HrPayrollErrorCodes.PayrollEntryNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelPayrollCommand(companyId, id, postingDate, rowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return PayrollProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>Lists the company's payroll batch headers, newest first. Read-only.</summary>
    /// <param name="companyId">Company that owns the batches.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollEntryDto>), StatusCodes.Status200OK)]
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
                HrPayrollErrorCodes.CompanyNotFound);
        }

        var entries = await _sender.SendAsync(new GetPayrollEntriesQuery(companyId), cancellationToken);
        return Ok(entries);
    }

    /// <summary>Reads one batch with every slip and its itemized lines. Read-only.</summary>
    /// <param name="id">Payroll entry id.</param>
    /// <param name="companyId">Company that owns the entry.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayrollEntryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Payroll Entry",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                HrPayrollErrorCodes.PayrollEntryNotFound);
        }

        var detail = await _sender.SendAsync(new GetPayrollEntryQuery(companyId, id), cancellationToken);
        if (detail is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                "Payroll Entry Not Found",
                $"Payroll entry '{id}' was not found in company '{companyId}'.",
                HrPayrollErrorCodes.PayrollEntryNotFound);
        }

        return Ok(detail);
    }

    /// <summary>
    /// RFC 7807 mapping for the payroll batch routes: missing entries/companies are 404, state
    /// conflicts (bad transition, frozen period, concurrency race) are 409, every other domain
    /// failure is a 400 carrying the stable machine code.
    /// </summary>
    private ObjectResult PayrollProblem(Error error) =>
        error.Code switch
        {
            HrPayrollErrorCodes.PayrollEntryNotFound
                or HrPayrollErrorCodes.CompanyNotFound
                or HrPayrollErrorCodes.StructureNotFound
                or HrPayrollErrorCodes.ComponentNotFound
                or HrPayrollErrorCodes.EmployeeNotFound
                or HrPayrollErrorCodes.BankAccountNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    "Payroll Resource Not Found",
                    error.Message,
                    error.Code),
            HrPayrollErrorCodes.InvalidStatusTransition
                or HrPayrollErrorCodes.DuplicateSalarySlip
                or HrPayrollErrorCodes.PayrollPeriodOverlap
                or AccountingErrorCodes.FiscalPeriodLocked
                or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    "Payroll Conflict",
                    error.Message,
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "Payroll Rejected",
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
