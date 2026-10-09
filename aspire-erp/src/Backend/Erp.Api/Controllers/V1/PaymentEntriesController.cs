using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Payments.Commands;
using Erp.Application.Features.Payments.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Payment voucher endpoints (spec R-12): Draft creation, the idempotent submit posting and
/// the compensating cancel, plus the list/detail reads. Every attribute follows Constitution
/// Article VI: explicit route + versioning + TenantMember policy (VI.1), content negotiation
/// (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// Submit and cancel carry <c>[IdempotencyKeyRequired]</c> (Article VI.4: ledger-posting
/// mutations): a replayed submit returns the first response verbatim instead of posting twice.
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/payment-entries")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class PaymentEntriesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public PaymentEntriesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's payment vouchers, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PaymentEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] PaymentDocumentStatus? status = null,
        [FromQuery] PaymentType? paymentType = null,
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

        var payments = await _sender.SendAsync(
            new GetPaymentsQuery(companyId, page, pageSize, status, paymentType), cancellationToken);
        return Ok(payments);
    }

    /// <summary>Returns one payment voucher with its allocation slices.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentEntryDetailDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidPayment"),
                _errors.Text(BankingErrorCodes.PaymentNotFound),
                BankingErrorCodes.PaymentNotFound);
        }

        var detail = await _sender.SendAsync(new GetPaymentDetailQuery(companyId, id), cancellationToken);
        return detail is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("PaymentNotFound"),
                _errors.Text(BankingErrorCodes.PaymentNotFound),
                BankingErrorCodes.PaymentNotFound)
            : Ok(detail);
    }

    /// <summary>Creates one Draft payment voucher (zero GL impact until submit).</summary>
    [HttpPost]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PaymentEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentEntryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("PaymentRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, companyId = dto.CompanyId }, dto);
    }

    /// <summary>Submits a Draft voucher: gapless number, GL settlement, invoice settlement.</summary>
    [HttpPost("{id:guid}/submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PaymentEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] SubmitPaymentBody body,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidPayment"),
                _errors.Text(BankingErrorCodes.PaymentNotFound),
                BankingErrorCodes.PaymentNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitPaymentEntryCommand(id, companyId, body?.RowVersion), cancellationToken);

        if (!result.IsSuccess)
        {
            return PaymentProblem(result.Error!);
        }

        return Ok(result.Value!);
    }

    /// <summary>Cancels a Submitted voucher via compensating reversal.</summary>
    [HttpPost("{id:guid}/cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(PaymentEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] CancelPaymentBody body,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidPayment"),
                _errors.Text(BankingErrorCodes.PaymentNotFound),
                BankingErrorCodes.PaymentNotFound);
        }

        var result = await _sender.SendAsync(
            new CancelPaymentEntryCommand(id, companyId, body?.RowVersion), cancellationToken);

        if (!result.IsSuccess)
        {
            return PaymentProblem(result.Error!);
        }

        return Ok(result.Value!);
    }

    private ObjectResult PaymentProblem(Error error) => error.Code switch
    {
        BankingErrorCodes.PaymentNotFound or BankingErrorCodes.CompanyNotFound => Problem(
            StatusCodes.Status404NotFound,
            _common.Text("PaymentNotFound"),
            _errors.Text(error.Code, error.Message),
            error.Code),
        ConcurrencyErrorCodes.ConcurrencyConflict or BankingErrorCodes.OverAllocation => Problem(
            StatusCodes.Status409Conflict,
            _common.Text("PaymentConflict"),
            error.Message,
            error.Code),
        _ => Problem(
            StatusCodes.Status400BadRequest,
            _common.Text("PaymentRejected"),
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
public sealed record SubmitPaymentBody(byte[]? RowVersion);

/// <summary>Cancel body: the optional client RowVersion for the compare-and-swap.</summary>
public sealed record CancelPaymentBody(byte[]? RowVersion);
