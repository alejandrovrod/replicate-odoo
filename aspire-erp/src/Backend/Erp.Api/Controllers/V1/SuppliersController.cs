using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Buying.Commands;
using Erp.Application.Features.Buying.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Supplier master endpoints (Task 4.1: codes unique per TENANT). No
/// <c>[IdempotencyKeyRequired]</c> here: Article VI.4 scopes that guard to ledger-posting mutations
/// and creating a supplier touches only the Supplier table.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SuppliersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the tenant's most recent suppliers.</summary>
    /// <param name="limit">Maximum number of suppliers to return (defaults to 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _sender.SendAsync(new GetSuppliersQuery(limit), cancellationToken);
        return Ok(suppliers);
    }

    /// <summary>Creates one supplier (unique code per tenant - duplicates are a 409).</summary>
    /// <param name="command">Supplier data (code, name, active flag).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate code conflicts with the existing master, anything else is a
            // field/lookup rejection carrying the stable machine code.
            return error.Code switch
            {
                PurchaseErrorCodes.DuplicateSupplierCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateSupplierCode"),
                    // Instance-valued detail (the colliding code): passes through
                    // untranslated, the title still localizes (Phase 2 convention).
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SupplierRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return CreatedAtAction(nameof(Get), new { }, result.Value);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
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
