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
/// Sales order endpoints (Task 5.2): create in Draft with the gapless SO-YYYY-NNNNN number and
/// the Draft -&gt; Submitted transition that runs the spec SL-02 credit gate.
/// </summary>
/// <remarks>
/// <para>
/// No <c>[IdempotencyKeyRequired]</c> on either action: neither writes StockLedgerEntry nor
/// GLEntry, so Article VI.4 does not apply - workflow conflicts
/// (<c>invalid_status_transition</c>) and credit breaches surface as 409 instead.
/// </para>
/// <para><b>Two route templates (constitution VI.1 vs the kebab-case document path).</b>
/// Constitution Article VI.1 mandates <c>[Route("api/v1/[controller]")]</c>, which yields
/// <c>/api/v1/SalesOrders</c>; the selling documents are addressed at
/// <c>/api/v1/sales-orders</c>. Route matching is case-insensitive but NOT hyphen-insensitive, so
/// the controller declares BOTH templates (the JournalEntriesController precedent) and neither
/// authority is violated. No path matches both templates.</para>
/// <para><b>Status mapping</b> (the established pipeline): <c>invalid_status_transition</c>,
/// <c>credit_limit_exceeded</c> and <c>concurrency_conflict</c> are STATE conflicts -&gt; 409;
/// <c>sales_order_not_found</c> -&gt; 404; everything else describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/sales-orders")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SalesOrdersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's most recent sales orders with their lines.</summary>
    /// <param name="companyId">Company that owns the orders.</param>
    /// <param name="limit">Maximum number of orders to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SalesOrderDto>), StatusCodes.Status200OK)]
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

        var orders = await _sender.SendAsync(new GetSalesOrdersQuery(companyId, page, pageSize), cancellationToken);
        return Ok(orders);
    }

    /// <summary>Returns one sales order (header + lines) by id.</summary>
    /// <param name="id">Sales order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidSalesOrder"),
                _errors.Text(SellingErrorCodes.SalesOrderNotFound),
                SellingErrorCodes.SalesOrderNotFound);
        }

        var order = await _sender.SendAsync(
            new GetSalesOrderByIdQuery(companyId, id), cancellationToken);

        return order is null
            ? NotFoundProblem(id)
            : Ok(order);
    }

    /// <summary>
    /// Creates one sales order in Draft with its gapless SO number (no stock/GL impact). Totals
    /// are computed server-side from the lines: no tax engine yet, so TaxTotal stays 0.0000.
    /// </summary>
    /// <param name="command">Order data (company, customer, dates, lines).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalesOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        var order = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = order.CompanyId }, order);
    }

    /// <summary>
    /// Advances one Draft order to Submitted (Task 5.2 workflow) after the spec SL-02 credit gate.
    /// Any state other than Draft is a 409 (<c>invalid_status_transition</c>); a breached credit
    /// limit is a 409 (<c>credit_limit_exceeded</c>) and the order stays in Draft.
    /// </summary>
    /// <param name="id">Sales order id.</param>
    /// <param name="companyId">Company that owns the order.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidSalesOrder"),
                _errors.Text(SellingErrorCodes.SalesOrderNotFound),
                SellingErrorCodes.SalesOrderNotFound);
        }

        var result = await _sender.SendAsync(
            new SubmitSalesOrderCommand(companyId, id), cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>Maps one command outcome to RFC 7807 (see the class remarks for the matrix).</summary>
    private ObjectResult ToActionResult(Result<SalesOrderDto> result)
    {
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                SellingErrorCodes.SalesOrderNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("SalesOrderNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                SellingErrorCodes.InvalidStatusTransition
                    or SellingErrorCodes.CreditLimitExceeded
                    or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    error.Code switch
                    {
                        SellingErrorCodes.CreditLimitExceeded => _common.Text("CreditLimitExceeded"),
                        ConcurrencyErrorCodes.ConcurrencyConflict => _common.Text("ConcurrentUpdateConflict"),
                        _ => _common.Text("SalesOrderConflict"),
                    },
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SalesOrderRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value);
    }

    /// <summary>RFC 7807 404 for an order that does not exist (or is not visible) in this tenant.</summary>
    private ObjectResult NotFoundProblem(Guid id) =>
        Problem(
            StatusCodes.Status404NotFound,
            _common.Text("SalesOrderNotFound"),
            _errors.Text(SellingErrorCodes.SalesOrderNotFound),
            SellingErrorCodes.SalesOrderNotFound);

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
