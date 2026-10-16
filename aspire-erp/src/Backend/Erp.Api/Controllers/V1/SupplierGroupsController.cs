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
/// Supplier group catalog endpoints (module buying-suppliers, spec §5
/// <c>GET /api/v1/supplier-groups</c>). No idempotency guard: creating a group touches only the
/// SupplierGroup table (Article VI.4 scopes the guard to ledger-posting mutations).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Route("api/v1/supplier-groups")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class SupplierGroupsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SupplierGroupsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the tenant's supplier group catalog (active first, then alphabetical).</summary>
    [HttpGet]
    [Authorize(Policy = "permission:supplier:read")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var groups = await _sender.SendAsync(new GetSupplierGroupsQuery(), cancellationToken);
        return Ok(groups);
    }

    /// <summary>Creates one supplier group (unique code per tenant - duplicates are a 409).</summary>
    [HttpPost]
    [Authorize(Policy = "permission:supplier:write")]
    [ProducesResponseType(typeof(SupplierGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierGroupCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                PurchaseErrorCodes.DuplicateSupplierGroupCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateSupplierGroupCode"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("SupplierGroupRejected"),
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
