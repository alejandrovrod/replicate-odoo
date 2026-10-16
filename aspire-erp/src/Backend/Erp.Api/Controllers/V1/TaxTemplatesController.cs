using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Tax.Commands;
using Erp.Application.Features.Tax.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Tax template catalog endpoints (tax-engine Fase 2): company-scoped reusable tax
/// packages ("Honorarios PM a PF": IVA 16% + retentions). Route resolves to
/// <c>/api/v1/taxtemplates</c>. No <c>[IdempotencyKeyRequired]</c>: catalog rows are
/// config, never ledger postings (CustomersController precedent).
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b>: <c>duplicate_tax_template</c> -&gt; 409;
/// <c>tax_template_not_found</c> -&gt; 404; everything else
/// (<c>tax_account_required</c>, <c>invalid_tax_calculation_basis</c>,
/// <c>invalid_tax_template_category</c>, ...) describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class TaxTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public TaxTemplatesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the active templates of one company and category (UI selector source).</summary>
    /// <param name="companyId">Company owning the templates.</param>
    /// <param name="category">Template flow: "Sales" or "Purchase".</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:tax_template:read")]
    [ProducesResponseType(typeof(IReadOnlyList<TaxTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        [FromQuery] string category = "Sales",
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidTaxTemplate"),
                _errors.Text(TaxErrorCodes.TaxTemplateNotFound),
                TaxErrorCodes.TaxTemplateNotFound);
        }

        var templates = await _sender.SendAsync(
            new GetTaxTemplatesQuery(companyId, category), cancellationToken);
        return Ok(templates);
    }

    /// <summary>Returns one template with its lines.</summary>
    /// <param name="id">Template id.</param>
    /// <param name="companyId">Company owning the template.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:tax_template:read")]
    [ProducesResponseType(typeof(TaxTemplateDto), StatusCodes.Status200OK)]
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
                _common.Text("InvalidTaxTemplate"),
                _errors.Text(TaxErrorCodes.TaxTemplateNotFound),
                TaxErrorCodes.TaxTemplateNotFound);
        }

        var template = await _sender.SendAsync(new GetTaxTemplateByIdQuery(companyId, id), cancellationToken);

        return template is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("TaxTemplateNotFound"),
                _errors.Text(TaxErrorCodes.TaxTemplateNotFound),
                TaxErrorCodes.TaxTemplateNotFound)
            : Ok(template);
    }

    /// <summary>Registers one tax template (duplicate titles per company+category are a 409).</summary>
    /// <param name="command">Title, category, default flag and lines.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:tax_template:write")]
    [ProducesResponseType(typeof(TaxTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaxTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                TaxErrorCodes.DuplicateTaxTemplate => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("TaxTemplateConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("TaxTemplateRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, companyId = dto.CompanyId }, dto);
    }

    /// <summary>Replaces one template (title/category/default + wholesale line replacement).</summary>
    /// <param name="id">Template ID.</param>
    /// <param name="command">Updated data including the same ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "permission:tax_template:write")]
    [ProducesResponseType(typeof(TaxTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaxTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidTaxTemplate"),
                _errors.Text(TaxErrorCodes.TaxTemplateNotFound),
                TaxErrorCodes.TaxTemplateNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                TaxErrorCodes.TaxTemplateNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("TaxTemplateNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                TaxErrorCodes.DuplicateTaxTemplate => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("TaxTemplateConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("TaxTemplateRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Deletes one catalog template with its lines (config, never ledger data).</summary>
    /// <param name="id">Template ID.</param>
    /// <param name="companyId">Company owning the template.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "permission:tax_template:write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(new DeleteTaxTemplateCommand(id, companyId), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("TaxTemplateNotFound"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        return NoContent();
    }

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
