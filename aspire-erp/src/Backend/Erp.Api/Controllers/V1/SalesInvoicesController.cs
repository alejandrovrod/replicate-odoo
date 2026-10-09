using Erp.Api.Common;
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
/// Sales invoice endpoints: list/detail reads, Draft creation and the Draft -&gt; Unpaid
/// submit transition (credit gate + A/R + revenue postings), plus the POS cashier shortcut.
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b> (the established pipeline):
/// <c>validation_failed</c> for a missing invoice -&gt; 404;
/// <c>credit_limit_exceeded</c> and <c>concurrency_conflict</c> are STATE conflicts -&gt; 409;
/// everything else describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/sales-invoices")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class SalesInvoicesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SalesInvoicesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent sales invoices with their lines.</summary>
    /// <param name="companyId">Company that owns the invoices.</param>
    /// <param name="page">Page number (defaults to 1).</param>
    /// <param name="pageSize">Page size (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SalesInvoiceDto>), StatusCodes.Status200OK)]
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

        var invoices = await _sender.SendAsync(new GetSalesInvoicesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(invoices);
    }

    /// <summary>Returns one sales invoice (header + lines) by id.</summary>
    /// <param name="id">Sales invoice id.</param>
    /// <param name="companyId">Company that owns the invoice.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SalesInvoiceDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidSalesInvoice"),
                _errors.Text(SellingErrorCodes.ValidationFailed),
                SellingErrorCodes.ValidationFailed);
        }

        var invoice = await _sender.SendAsync(
            new GetSalesInvoiceByIdQuery(companyId, id), cancellationToken);

        return invoice is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("SalesInvoiceNotFound"),
                _errors.Text(SellingErrorCodes.ValidationFailed),
                SellingErrorCodes.ValidationFailed)
            : Ok(invoice);
    }

    /// <summary>
    /// Creates one sales invoice in Draft (no GL impact). Totals are computed server-side
    /// from the lines; the due date derives from the customer's payment terms.
    /// </summary>
    /// <param name="command">Invoice data (company, customer, posting date, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SalesInvoiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalesInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result, created: true);
        }

        var invoice = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id, companyId = invoice.CompanyId }, invoice);
    }

    /// <summary>
    /// Advances one Draft invoice to Unpaid after the spec SL-02 credit gate, booking
    /// Dr Accounts Receivable / Cr revenue. A breached credit limit is a 409 and the
    /// invoice stays in Draft.
    /// </summary>
    /// <param name="id">Sales invoice id.</param>
    /// <param name="companyId">Company that owns the invoice.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(SalesInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidSalesInvoice"),
                _errors.Text(SellingErrorCodes.ValidationFailed),
                SellingErrorCodes.ValidationFailed);
        }

        var result = await _sender.SendAsync(
            new SubmitSalesInvoiceCommand(companyId, id), cancellationToken);

        return ToActionResult(result, created: false);
    }

    [HttpPost("pos")]
    [ProducesResponseType(typeof(SalesInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SalesInvoiceDto>> SubmitPOSInvoice(
        [FromBody] SubmitPOSInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            // Same RFC 7807 shape as every other rejection: the previous
            // `BadRequest(new { result.Error })` body was unreadable by the shared
            // `apiClient` interceptor, so its message never reached the UI.
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = _common.Text("SalesInvoiceRejected"),
                Status = StatusCodes.Status400BadRequest,
                Detail = _errors.Text(error.Code, error.Message),
                Instance = HttpContext.Request.Path.Value,
            };
            problem.Extensions["code"] = error.Code;
            return BadRequest(problem);
        }

        return Ok(result.Value);
    }

    /// <summary>Maps one command outcome to RFC 7807.</summary>
    private ObjectResult ToActionResult(Result<SalesInvoiceDto> result, bool created)
    {
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                SellingErrorCodes.ValidationFailed => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("SalesInvoiceNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                SellingErrorCodes.CompanyNotFound
                    or SellingErrorCodes.CustomerNotFound
                    or SellingErrorCodes.ItemNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("SalesInvoiceNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                SellingErrorCodes.CreditLimitExceeded
                    or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    error.Code switch
                    {
                        SellingErrorCodes.CreditLimitExceeded => _common.Text("CreditLimitExceeded"),
                        _ => _common.Text("SalesInvoiceConflict"),
                    },
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SalesInvoiceRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return created
            ? new ObjectResult(result.Value) { StatusCode = StatusCodes.Status201Created }
            : Ok(result.Value);
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
