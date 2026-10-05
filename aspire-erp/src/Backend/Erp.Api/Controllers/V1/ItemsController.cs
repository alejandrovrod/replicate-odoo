using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Items.Commands;
using Erp.Application.Features.Items.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Item / SKU endpoints (Task 3.1). Every attribute below is mandated by Constitution Article VI:
/// explicit route + versioning + TenantMember policy (VI.1), content negotiation (VI.2) and
/// exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// No [IdempotencyKeyRequired] here: Article VI.4 scopes that guard to ledger-posting mutations,
/// and creating an item writes no GLEntry row (decision D7). Duplicate SKUs are rejected per
/// tenant, so they surface as 409 Conflict (DoD 3.1).
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class ItemsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public ItemsController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>
    /// Returns the tenant's items together with their stock inside the given company's warehouses.
    /// </summary>
    /// <param name="companyId">Company whose warehouses bound the stock breakdown.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCompany"),
                _errors.Text("company_required"),
                "company_required");
        }

        var items = await _sender.SendAsync(new GetItemsQuery(companyId), cancellationToken);
        return Ok(items);
    }

    /// <summary>Creates one SKU. 201 with the created item; a duplicate SKU is a 409.</summary>
    /// <param name="command">Item data (code, name, valuation method, base UOM).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateItemCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: a duplicate SKU is a conflict, every other domain failure is a bad request.
            return error.Code switch
            {
                StockErrorCodes.DuplicateItemCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("ItemConflict"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("ItemRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return Created("/api/v1/items", dto);
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
