using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Services;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>One engine input row on the wire (mirrors <see cref="TaxRowInput"/>).</summary>
public sealed record TaxCalculateLine(
    decimal Rate,
    TaxChargeType ChargeType = TaxChargeType.Add,
    TaxCalculationBasis CalculationBasis = TaxCalculationBasis.OnNetTotal,
    decimal FixedAmount = 0m);

/// <summary>Preview request: taxable base + ordered rows (no persistence).</summary>
public sealed record TaxCalculateRequest(
    decimal NetBase,
    IReadOnlyList<TaxCalculateLine> Lines,
    int Decimals = 2);

/// <summary>One computed row of the preview.</summary>
public sealed record TaxCalculateRow(
    decimal BaseAmount,
    decimal TaxAmount,
    TaxChargeType ChargeType,
    TaxCalculationBasis CalculationBasis,
    decimal Rate);

/// <summary>Deterministic preview result (mirrors <see cref="TaxCalculationResult"/>).</summary>
public sealed record TaxCalculateResponse(
    IReadOnlyList<TaxCalculateRow> Rows,
    decimal AddTotal,
    decimal DeductTotal,
    decimal TaxTotal,
    decimal GrandTotal);

/// <summary>
/// Tax engine preview endpoint (tax-engine Fase 2, T4): runs the deterministic calculation
/// WITHOUT persisting anything, so invoice UIs render live breakdowns and totals. Pure
/// function of the request - no database access.
/// </summary>
[ApiController]
[Route("api/v1/tax-engine")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class TaxEngineController : ControllerBase
{
    private readonly ITaxEngineService _engine;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public TaxEngineController(
        ITaxEngineService engine,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _engine = engine;
        _errors = errors;
        _common = common;
    }

    /// <summary>Calculates rows and totals for a base + ordered tax lines (no persistence).</summary>
    /// <param name="request">Net base, ordered rows and rounding decimals (0-6).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("calculate")]
    [Authorize(Policy = "permission:tax_template:read")]
    [ProducesResponseType(typeof(TaxCalculateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Calculate(
        [FromBody] TaxCalculateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = _engine.Calculate(
                request.NetBase,
                request.Lines
                    .Select(l => new TaxRowInput(l.Rate, l.ChargeType, l.CalculationBasis, l.FixedAmount))
                    .ToList(),
                request.Decimals);

            return Ok(new TaxCalculateResponse(
                result.Rows
                    .Select(r => new TaxCalculateRow(r.BaseAmount, r.TaxAmount, r.ChargeType, r.CalculationBasis, r.Rate))
                    .ToList(),
                result.AddTotal,
                result.DeductTotal,
                result.TaxTotal,
                result.GrandTotal));
        }
        catch (TaxValidationException ex)
        {
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = _common.Text("TaxCalculationRejected"),
                Status = StatusCodes.Status400BadRequest,
                Detail = _errors.Text(ex.Code, ex.Message),
                Instance = HttpContext.Request.Path.Value,
            };
            problem.Extensions["code"] = ex.Code;
            return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
