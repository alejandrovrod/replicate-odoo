using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.LandedCost.Commands;
using Erp.Application.Features.LandedCost.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Landed cost voucher endpoints (Fase 4): Draft creation with pre-calculated
/// apportionment, idempotent submit posting (inventory uplift + Kardex revaluation) and
/// compensating cancel.
/// </summary>
/// <remarks>
/// Submit and cancel carry <c>[IdempotencyKeyRequired]</c> (Article VI.4: ledger-posting
/// mutations): a replayed submit returns the first response verbatim instead of posting
/// twice. Draft creation writes no ledger rows, so it needs no idempotency guard
/// (CustomersController precedent).
/// </remarks>
[ApiController]
[Route("api/v1/landed-cost-vouchers")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class LandedCostVouchersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public LandedCostVouchersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's landed cost vouchers, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = "permission:landed_cost_voucher:read")]
    [ProducesResponseType(typeof(PagedResult<LandedCostVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DocumentStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(LandedCostErrorCodes.LandedCostVoucherNotFound),
                LandedCostErrorCodes.LandedCostVoucherNotFound);
        }

        var vouchers = await _sender.SendAsync(
            new GetLandedCostVouchersQuery(companyId, page, pageSize, status), cancellationToken);
        return Ok(vouchers);
    }

    /// <summary>Returns one voucher with references, charges and apportionment.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:landed_cost_voucher:read")]
    [ProducesResponseType(typeof(LandedCostVoucherDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidLandedCostVoucher"),
                _errors.Text(LandedCostErrorCodes.LandedCostVoucherNotFound),
                LandedCostErrorCodes.LandedCostVoucherNotFound);
        }

        var voucher = await _sender.SendAsync(new GetLandedCostVoucherByIdQuery(companyId, id), cancellationToken);

        return voucher is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("LandedCostVoucherNotFound"),
                _errors.Text(LandedCostErrorCodes.LandedCostVoucherNotFound),
                LandedCostErrorCodes.LandedCostVoucherNotFound)
            : Ok(voucher);
    }

    /// <summary>Creates one Draft voucher with pre-calculated apportionment (no ledger impact).</summary>
    [HttpPost]
    [Authorize(Policy = "permission:landed_cost_voucher:write")]
    [ProducesResponseType(typeof(LandedCostVoucherDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLandedCostVoucherCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("LandedCostVoucherRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, companyId = dto.CompanyId }, dto);
    }

    /// <summary>Submits a Draft voucher: gapless number, inventory uplift GL + Kardex revaluation.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "permission:landed_cost_voucher:submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(LandedCostVoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] SubmitLandedCostBody body,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidLandedCostVoucher"),
                _errors.Text(LandedCostErrorCodes.LandedCostVoucherNotFound),
                LandedCostErrorCodes.LandedCostVoucherNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitLandedCostVoucherCommand(companyId, id, body?.RowVersion), cancellationToken);

        return result.IsSuccess ? Ok(result.Value!) : VoucherProblem(result.Error!);
    }

    /// <summary>Cancels a Submitted voucher via compensating reversal.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "permission:landed_cost_voucher:cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(LandedCostVoucherDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] CancelLandedCostBody body,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidLandedCostVoucher"),
                _errors.Text(LandedCostErrorCodes.LandedCostVoucherNotFound),
                LandedCostErrorCodes.LandedCostVoucherNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelLandedCostVoucherCommand(companyId, id, body?.RowVersion), cancellationToken);

        return result.IsSuccess ? Ok(result.Value!) : VoucherProblem(result.Error!);
    }

    private ObjectResult VoucherProblem(Error error) => error.Code switch
    {
        LandedCostErrorCodes.LandedCostVoucherNotFound => Problem(
            StatusCodes.Status404NotFound,
            _common.Text("LandedCostVoucherNotFound"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        ConcurrencyErrorCodes.ConcurrencyConflict or LandedCostErrorCodes.InvalidStatusTransition => Problem(
            StatusCodes.Status409Conflict,
            _common.Text("LandedCostVoucherConflict"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        _ => Problem(
            StatusCodes.Status400BadRequest,
            _common.Text("LandedCostVoucherRejected"),
            _errors.Text(error.Code, error.Message),
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

/// <summary>Submit body: the optional client RowVersion for the compare-and-swap.</summary>
public sealed record SubmitLandedCostBody(byte[]? RowVersion);

/// <summary>Cancel body: the optional client RowVersion for the compare-and-swap.</summary>
public sealed record CancelLandedCostBody(byte[]? RowVersion);
