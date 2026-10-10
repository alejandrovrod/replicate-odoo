using Erp.Api.Common;
using Erp.Api.Filters;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.FiscalClosing;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Fiscal year master endpoints (R-13, plan.md §5): create an open year, list with
/// <c>IsClosed</c> filter, and hard-lock the year. All mutating routes require the
/// <c>Idempotency-Key</c> header; close additionally requires the current <c>RowVersion</c>.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/fiscal-years")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class FiscalYearsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public FiscalYearsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Creates one OPEN fiscal year (no overlap with the same company's years).</summary>
    [HttpPost]
    [Authorize(Policy = "permission:fiscal_year:write")]
    [Consumes("application/json")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(FiscalYearDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFiscalYearRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(request.YearName))
        {
            return FiscalProblem(new Error(
                FiscalClosingErrorCodes.ClosingDateOutsideFiscalYear,
                "Company id and fiscal year name are required."));
        }

        var result = await _sender.SendAsync(
            new CreateFiscalYearCommand(request.CompanyId, request.YearName, request.StartDate, request.EndDate),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return FiscalProblem(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id, companyId = result.Value.CompanyId }, result.Value);
    }

    /// <summary>Paged fiscal-year list with an optional <c>IsClosed</c> filter.</summary>
    [HttpGet]
    [Authorize(Policy = "permission:fiscal_year:read")]
    [ProducesResponseType(typeof(PagedResult<FiscalYearDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid companyId,
        [FromQuery] bool? isClosed = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var years = await _sender.SendAsync(
            new GetFiscalYearsQuery(companyId, isClosed, page, pageSize), cancellationToken);
        return Ok(years);
    }

    /// <summary>Returns one fiscal year by id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:fiscal_year:read")]
    [ProducesResponseType(typeof(FiscalYearDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        var years = await _sender.SendAsync(
            new GetFiscalYearsQuery(companyId, null, 1, int.MaxValue), cancellationToken);
        var year = years.Items.FirstOrDefault(y => y.Id == id);
        return year is null
            ? FiscalProblem(new Error(
                FiscalClosingErrorCodes.FiscalYearNotFound,
                $"Fiscal year '{id}' was not found in this tenant."))
            : Ok(year);
    }

    /// <summary>
    /// Hard-locks the year (<c>IsClosed = 1</c>). Refused while a Draft voucher is pending;
    /// re-open does not exist.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "permission:fiscal_year:submit")]
    [IdempotencyKeyRequired]
    [ProducesResponseType(typeof(FiscalYearDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CloseFiscalYearRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || request.CompanyId == Guid.Empty)
        {
            return FiscalProblem(new Error(
                FiscalClosingErrorCodes.FiscalYearNotFound,
                "Fiscal year id and company id are required."));
        }

        var result = await _sender.SendAsync(
            new CloseFiscalYearCommand(id, request.CompanyId, request.RowVersion), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : FiscalProblem(result.Error!);
    }

    private ObjectResult FiscalProblem(Error error) =>
        FiscalProblem(FiscalClosingProblemMap.StatusFor(error.Code), error.Code, error.Message);

    private ObjectResult FiscalProblem(int status, string code, string detail)
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
                StatusCodes.Status404NotFound => _common.Text("FiscalYearNotFound"),
                StatusCodes.Status409Conflict => _common.Text("FiscalYearConflict"),
                StatusCodes.Status422UnprocessableEntity => _common.Text("FiscalYearRejected"),
                StatusCodes.Status500InternalServerError => _common.Text("FiscalYearRejected"),
                _ => _common.Text("FiscalYearRejected"),
            },
            Status = status,
            Detail = _errors.Text(code, detail),
            Instance = HttpContext.Request.Path.Value,
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

/// <summary>RFC 7807 status mapping for every spec §5 code (delegates to Application).</summary>
public static class FiscalClosingProblemMap
{
    public static int StatusFor(string code) => FiscalClosingHttpStatus.StatusFor(code);
}

public sealed record CreateFiscalYearRequest(
    Guid CompanyId,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CloseFiscalYearRequest(Guid CompanyId, byte[]? RowVersion = null);
