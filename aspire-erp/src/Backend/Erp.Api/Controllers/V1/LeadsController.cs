using Erp.Api.Filters;
using Erp.Application.Common;
using Erp.Application.Features.Crm.Commands;
using Erp.Application.Features.Crm.DTOs;
using Erp.Application.Features.Crm.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>Lead conversion body: customer/opportunity terms for the convert route.</summary>
public sealed record ConvertLeadRequest(
    string CustomerCode,
    string? DefaultCurrency = "USD",
    int PaymentTermsDays = 30,
    decimal OpportunityAmount = 0m,
    decimal OpportunityProbability = 10m,
    DateOnly? ExpectedClosingDate = null,
    Guid? ConvertedByUserId = null);

/// <summary>
/// Lead endpoints (Block B, tasks 11.4/11.6): idempotent webhook intake, lead-to-customer
/// conversion and the list read. Attributes follow Constitution Article VI: explicit route +
/// versioning + TenantMember policy (VI.1) and exhaustive status documentation (VI.3). The
/// two row-creating mutations carry the literal <c>[IdempotencyKeyRequired]</c> guard
/// (Article VI.4); the list read writes nothing, so no filter applies to it.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class LeadsController : ControllerBase
{
    private readonly ISender _sender;

    public LeadsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's most recent leads.</summary>
    /// <param name="companyId">Company that owns the leads.</param>
    /// <param name="limit">Maximum number of leads to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
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
                CRMErrorCodes.CompanyRequired);
        }

        var leads = await _sender.SendAsync(new GetLeadsQuery(companyId, limit), cancellationToken);
        return Ok(leads);
    }

    /// <summary>
    /// Ingests one inbound lead (spec CRM-04). A replayed <c>DeduplicationKey</c> returns the
    /// existing lead with <c>Duplicate=true</c> (HTTP 200, zero new rows); a fresh lead is
    /// created (HTTP 201).
    /// </summary>
    /// <remarks>
    /// Requires the <c>Idempotency-Key</c> header (Constitution VI.4): a missing header is a
    /// 400, a replayed key returns the stored response verbatim, and reusing a key with a
    /// different payload is a 409. Body-level webhook replay safety (spec CRM-04) rides the
    /// <c>DeduplicationKey</c> triple independently of that header.
    /// </remarks>
    /// <param name="command">Lead data (company, code, name, contact, source, dedup key).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("ingest")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(IngestLeadResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IngestLeadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(
        [FromBody] IngestLeadCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                "Lead Rejected",
                error.Message,
                error.Code);
        }

        var ingest = result.Value!;
        return ingest.Duplicate
            ? Ok(ingest)
            : CreatedAtAction(nameof(List), new { companyId = ingest.Lead.CompanyId }, ingest);
    }

    /// <summary>
    /// Converts one qualified lead into a Customer and an Opportunity (spec CRM-03), copying
    /// a conversion note onto the new opportunity (task 11.4 audit trail).
    /// </summary>
    /// <remarks>Requires the <c>Idempotency-Key</c> header (Constitution VI.4) - the route creates customer and opportunity rows.</remarks>
    /// <param name="id">Lead id.</param>
    /// <param name="companyId">Company that owns the lead.</param>
    /// <param name="request">Customer/opportunity terms.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("{id:guid}/convert")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(ConvertLeadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Convert(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] ConvertLeadRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Lead",
                "Both the route id and the companyId query parameter must be non-empty GUIDs.",
                "crm_lead_not_found");
        }

        var result = await _sender.SendAsync(
            new ConvertLeadCommand(
                companyId,
                id,
                request.CustomerCode,
                request.DefaultCurrency,
                request.PaymentTermsDays,
                request.OpportunityAmount,
                request.OpportunityProbability,
                request.ExpectedClosingDate,
                request.ConvertedByUserId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return LeadProblem(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// RFC 7807 mapping for the lead routes: missing leads/companies are 404, the converted
    /// and duplicate-code conflicts are 409, every other domain failure is a 400 carrying
    /// the stable machine code.
    /// </summary>
    private ObjectResult LeadProblem(Error error) =>
        error.Code switch
        {
            "crm_lead_not_found"
                or "crm_opportunity_not_found"
                or "crm_lead_company_mismatch"
                or "crm_opportunity_company_mismatch"
                or CRMErrorCodes.CompanyRequired => Problem(
                    StatusCodes.Status404NotFound,
                    "Lead Resource Not Found",
                    error.Message,
                    error.Code),
            CRMErrorCodes.LeadAlreadyConverted
                or "crm_customer_code_exists" => Problem(
                    StatusCodes.Status409Conflict,
                    "Lead Conflict",
                    error.Message,
                    error.Code),
            _ => Problem(
                StatusCodes.Status400BadRequest,
                "Lead Rejected",
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
