using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.FiscalClosing;
using Erp.Application.Features.GeneralLedger.PeriodClosing;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Period Closing Voucher endpoints (R-13, plan.md §5): create a <c>Draft</c> bound to a fiscal
/// year, preview the P&amp;L lines submit would post, submit (atomic balanced close) and cancel
/// (compensating reversal). All mutating routes require the <c>Idempotency-Key</c> header;
/// submit/cancel additionally accept the current <c>RowVersion</c> for compare-and-swap.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/period-closing-vouchers")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class PeriodClosingVouchersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public PeriodClosingVouchersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Creates one <c>Draft</c> voucher bound to a fiscal year (no GL impact yet).</summary>
    [HttpPost]
    [Authorize(Policy = "permission:period_closing_voucher:write")]
    [Consumes("application/json")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PeriodClosingVoucherDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePeriodClosingVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || request.FiscalYearId == Guid.Empty)
        {
            return ClosingProblem(new Error(
                FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear,
                "Company id and fiscal year id are required."));
        }

        var result = await _sender.SendAsync(
            new CreatePeriodClosingVoucherCommand(
                request.CompanyId,
                request.FiscalYearId,
                request.PostingDate,
                request.RetainedEarningsAccountId ?? Guid.Empty,
                request.Remarks,
                IdempotencyKey()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ClosingProblem(result.Error!);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id, companyId = result.Value.CompanyId },
            result.Value);
    }

    /// <summary>Paged voucher list with fiscal-year + status filters.</summary>
    [HttpGet]
    [Authorize(Policy = "permission:period_closing_voucher:read")]
    [ProducesResponseType(typeof(PagedResult<PeriodClosingVoucherDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? fiscalYearId = null,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var vouchers = await _sender.SendAsync(
            new GetPeriodClosingVouchersQuery(companyId, fiscalYearId, status, page, pageSize),
            cancellationToken);
        return Ok(vouchers);
    }

    /// <summary>Returns one closing voucher (header + derived lines).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:period_closing_voucher:read")]
    [ProducesResponseType(typeof(PeriodClosingVoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        var voucher = await _sender.SendAsync(
            new GetPeriodClosingVoucherDetailQuery(id, companyId), cancellationToken);
        return voucher is null
            ? ClosingProblem(new Error(
                FiscalClosingErrorCodes.PeriodClosingNotFound,
                $"Closing voucher '{id}' was not found in this tenant."))
            : Ok(voucher);
    }

    /// <summary>
    /// Read-only pre-submit P&amp;L preview: the exact line set submit would post, from the same
    /// balance query. An empty year previews zero lines (submit itself rejects it, FC-12).
    /// </summary>
    [HttpGet("unclosed-balances")]
    [Authorize(Policy = "permission:period_closing_voucher:read")]
    [ProducesResponseType(typeof(ClosingPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        [FromQuery] Guid companyId,
        [FromQuery] Guid fiscalYearId,
        CancellationToken cancellationToken)
    {
        var preview = await _sender.SendAsync(
            new GetUnclosedPLBalancesQuery(companyId, fiscalYearId), cancellationToken);
        return preview is null
            ? ClosingProblem(new Error(
                FiscalClosingErrorCodes.FiscalYearNotFound,
                $"Fiscal year '{fiscalYearId}' was not found in this tenant."))
            : Ok(preview);
    }

    /// <summary>
    /// Submits one Draft voucher: computes the FY-windowed P&amp;L, validates the retained leaf
    /// and posts the balanced close in ONE serializable transaction. A same-key replay after
    /// commit returns the recorded success with zero new GL rows (FC-09).
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "permission:period_closing_voucher:submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PeriodClosingVoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ClosingTransitionRequest? request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return ClosingProblem(new Error(
                FiscalClosingErrorCodes.PeriodClosingNotFound,
                "Closing voucher id and company id are required."));
        }

        var result = await _sender.SendAsync(
            new SubmitPeriodClosingVoucherCommand(id, companyId, request?.RowVersion, IdempotencyKey()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ClosingProblem(result.Error!);
    }

    /// <summary>
    /// Cancels one Submitted voucher by appending the compensating reversal (originals untouched).
    /// Refused when the fiscal year is closed.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "permission:period_closing_voucher:cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PeriodClosingVoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ClosingTransitionRequest? request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return ClosingProblem(new Error(
                FiscalClosingErrorCodes.PeriodClosingNotFound,
                "Closing voucher id and company id are required."));
        }

        var result = await _sender.SendAsync(
            new CancelPeriodClosingVoucherCommand(id, companyId, request?.RowVersion, IdempotencyKey()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ClosingProblem(result.Error!);
    }

    /// <summary>The validated idempotency key (the filter guarantees presence on these routes).</summary>
    private string? IdempotencyKey() =>
        Request.Headers.TryGetValue(IdempotencyFilter.HeaderName, out var values)
            ? values.ToString().Trim()
            : null;

    private ObjectResult ClosingProblem(Error error) =>
        Problem(FiscalClosingProblemMap.StatusFor(error.Code), error.Code, error.Message);

    private ObjectResult Problem(int status, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc9110#section-15.5.22",
                StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            Title = status switch
            {
                StatusCodes.Status404NotFound => _common.Text("PeriodClosingNotFound"),
                StatusCodes.Status409Conflict => _common.Text("PeriodClosingConflict"),
                StatusCodes.Status422UnprocessableEntity => _common.Text("PeriodClosingRejected"),
                StatusCodes.Status500InternalServerError => _common.Text("PeriodClosingRejected"),
                _ => _common.Text("PeriodClosingRejected"),
            },
            Status = status,
            Detail = _errors.Text(code, detail),
            Instance = HttpContext.Request.Path.Value,
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

/// <summary>Optional body of the submit/cancel endpoints: the optimistic concurrency token.</summary>
public sealed record ClosingTransitionRequest(byte[]? RowVersion = null);

public sealed record CreatePeriodClosingVoucherRequest(
    Guid CompanyId,
    Guid FiscalYearId,
    DateOnly PostingDate,
    Guid? RetainedEarningsAccountId,
    string? Remarks);
