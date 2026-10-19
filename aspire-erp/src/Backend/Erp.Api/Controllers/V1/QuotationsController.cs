using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Features.Crm.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Formal quotation reads (CRM/ERPNext parity): quotations are born from
/// <c>POST /api/v1/opportunities/{id}/create-quotation</c> - this controller serves the
/// detail read behind the "quotation issued" confirmation.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class QuotationsController : ControllerBase
{
    private readonly IQuotationRepository _quotations;
    private readonly IItemRepository _items;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public QuotationsController(
        IQuotationRepository quotations,
        IItemRepository items,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _quotations = quotations;
        _items = items;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns one quotation (header + priced lines) by id.</summary>
    /// <param name="id">Quotation id.</param>
    /// <param name="companyId">Company that owns the quotation.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:opportunity:read")]
    [ProducesResponseType(typeof(QuotationDto), StatusCodes.Status200OK)]
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
                _common.Text("OpportunityRejected"),
                _errors.Text(CRMErrorCodes.QuotationNotFound),
                CRMErrorCodes.QuotationNotFound);
        }

        var quotation = await _quotations.GetByIdAsync(id, cancellationToken);
        if (quotation is null || quotation.CompanyId != companyId)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("OpportunityNotFound"),
                _errors.Text(CRMErrorCodes.QuotationNotFound),
                CRMErrorCodes.QuotationNotFound);
        }

        var names = (await _items.GetByIdsAsync(
                quotation.Items.Select(i => i.ItemId).Distinct().ToList(), cancellationToken))
            .ToDictionary(i => i.Id, i => i.ItemName);

        return Ok(QuotationDto.Build(quotation, names));
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
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
