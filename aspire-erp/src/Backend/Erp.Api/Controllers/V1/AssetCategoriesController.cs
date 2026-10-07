using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Assets.Commands;
using Erp.Application.Features.Assets.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Asset category endpoints (Tasks 10.1/Block B reads): create the GL account template (no
/// ledger impact, so no idempotency guard - the purchase-order precedent) and list one
/// company's templates. Attributes follow Constitution Article VI: explicit route + versioning
/// + TenantMember policy (VI.1) and exhaustive status documentation (VI.3).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
public sealed class AssetCategoriesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public AssetCategoriesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Creates one asset category with its GL account template links (no ledger impact).</summary>
    /// <param name="command">Category data (company, name, linked accounts).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssetCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("AssetCategoryRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        var category = result.Value!;
        return CreatedAtAction(nameof(List), new { companyId = category.CompanyId }, category);
    }

    /// <summary>Updates an existing asset category (Task 10.1 / RM-08).</summary>
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssetCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateAssetCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidRequest"),
                "Route ID does not match body ID.",
                "invalid_request");
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var status = error.Code == "concurrency_conflict"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;

            return Problem(
                status,
                _common.Text("AssetCategoryRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        return Ok(result.Value!);
    }

    /// <summary>Lists the company's asset categories. Read-only: no idempotency guard.</summary>
    /// <param name="companyId">Company that owns the categories.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AssetCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
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
                _errors.Text(AssetErrorCodes.CompanyNotFound),
                AssetErrorCodes.CompanyNotFound);
        }

        var categories = await _sender.SendAsync(new GetAssetCategoriesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(categories);
    }

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
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
