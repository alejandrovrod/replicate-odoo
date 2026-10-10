using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Crm.DTOs;
using Erp.Application.Features.Crm.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>Sales-order creation body: the single order line plus optional dates/author.</summary>
public sealed record CreateSalesOrderRequest(
    Guid ItemId,
    decimal Quantity,
    decimal Rate,
    DateOnly? TransactionDate = null,
    DateOnly? DeliveryDate = null,
    Guid? CreatedByUserId = null);

/// <summary>Stage-advance body: target stage plus optional probability, loss reason.</summary>
public sealed record AdvanceStageRequest(
    string ToStage,
    decimal? Probability = null,
    string? LossReason = null);

/// <summary>
/// Opportunity endpoints (Block B, tasks 11.2/11.5/11.6): the pipeline board/list read, the
/// Kanban stage-advance write (spec CRM-02) and the lost-deal re-open (spec CRM-05).
/// Attributes follow Constitution Article VI: explicit route + versioning + TenantMember
/// policy (VI.1) and exhaustive status documentation (VI.3). Both POST mutations carry the
/// literal <c>[IdempotencyKeyRequired]</c> guard (Article VI.4); the board read writes
/// nothing, so no filter applies to it.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class OpportunitiesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public OpportunitiesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the company's opportunities (the pipeline board source), newest first.</summary>
    /// <param name="companyId">Company that owns the opportunities.</param>
    /// <param name="limit">Maximum number of opportunities to return (defaults to 50).</param>
    /// <param name="stage">Optional stage filter (e.g. Negotiation).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:opportunity:read")]
    [ProducesResponseType(typeof(PagedResult<OpportunityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? stage = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text(CRMErrorCodes.CompanyRequired),
                CRMErrorCodes.CompanyRequired);
        }

        var opportunities = await _sender.SendAsync(
            new GetOpportunitiesQuery(companyId, page, pageSize, stage), cancellationToken);
        return Ok(opportunities);
    }

    /// <summary>
    /// Advances one opportunity to another pipeline stage (spec CRM-02 - the Kanban
    /// drag-drop write). ClosedWon forces 100% probability, ClosedLost forces 0% and
    /// requires a loss reason; closed deals are immutable here (409 - re-open instead).
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4).</remarks>
    /// <param name="id">Opportunity id.</param>
    /// <param name="companyId">Company that owns the opportunity.</param>
    /// <param name="request">Target stage plus optional probability/loss reason.</param>
    /// <param name="rowVersion">Optional optimistic token (base64) - a stale token fails fast.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/advance")]
    [Authorize(Policy = "permission:opportunity:submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Advance(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] AdvanceStageRequest request,
        [FromQuery] byte[]? rowVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidOpportunity"),
                _errors.Text("crm_opportunity_not_found"),
                "crm_opportunity_not_found");
        }

        var result = await _sender.SendAsync(
            new AdvanceOpportunityStageCommand(
                id,
                companyId,
                request.ToStage,
                request.Probability,
                request.LossReason,
                rowVersion),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return OpportunityProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Re-opens one ClosedLost opportunity back to Negotiation (spec CRM-05), delegating to
    /// the adopted re-open handler.
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4).</remarks>
    /// <param name="id">Opportunity id.</param>
    /// <param name="companyId">Company that owns the opportunity.</param>
    /// <param name="newProbability">Probability after re-opening (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = "permission:opportunity:cancel")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reopen(
        Guid id,
        [FromQuery] Guid companyId,
        [FromQuery] decimal newProbability = 50.00m,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidOpportunity"),
                _errors.Text("crm_opportunity_not_found"),
                "crm_opportunity_not_found");
        }

        var result = await _sender.SendAsync(
            new ReopenOpportunityCommand(companyId, id, newProbability),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return OpportunityProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Creates a formal sales order from a ClosedWon opportunity (spec CRM-02 1-click
    /// creation), delegating money and numbering to the existing selling creation path.
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - the route
    /// creates sales-order rows.</remarks>
    /// <param name="id">ClosedWon opportunity id.</param>
    /// <param name="companyId">Company that owns the opportunity.</param>
    /// <param name="request">Single order line (item/qty/rate) plus optional dates/author.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/create-sales-order")]
    [Authorize(Policy = "permission:opportunity:submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSalesOrder(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] CreateSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidOpportunity"),
                _errors.Text("crm_opportunity_not_found"),
                "crm_opportunity_not_found");
        }

        var result = await _sender.SendAsync(
            new CreateOpportunitySalesOrderCommand(
                companyId,
                id,
                request.ItemId,
                request.Quantity,
                request.Rate,
                request.TransactionDate,
                request.DeliveryDate,
                request.CreatedByUserId),
            cancellationToken);

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
                    _common.Text("SalesOrderConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => OpportunityProblem(error),
            };
        }

        var order = result.Value!;
        return Created(
            $"/api/v1/sales-orders/{order.Id}?companyId={order.CompanyId}",
            order);
    }

    /// <summary>
    /// RFC 7807 mapping for the opportunity routes: missing opportunities/companies are 404,
    /// state conflicts (terminal transition, concurrency race) are 409, every other domain
    /// failure - including the CRM-02 missing-loss-reason rejection - is a 400 carrying the
    /// stable machine code.
    /// </summary>
    private ObjectResult OpportunityProblem(Error error) =>
        error.Code switch
        {
            "crm_opportunity_not_found"
                or "crm_opportunity_company_mismatch"
                or CRMErrorCodes.CompanyRequired => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("OpportunityNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            CRMErrorCodes.InvalidStatusTransition
                or CRMErrorCodes.OpportunityAlreadyClosed
                or CRMErrorCodes.OpportunityNotWon
                or ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("OpportunityConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("OpportunityRejected"),
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
