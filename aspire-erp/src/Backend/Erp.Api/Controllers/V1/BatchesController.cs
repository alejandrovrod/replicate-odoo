using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Stock.Tracking;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Batch master endpoints (batch-and-serial Fase 3): manual registration, retire/disables
/// and the FEFO availability read. Route resolves to <c>/api/v1/batches</c>. Movement-born
/// batches (receipt auto-create/top-up) need no endpoint - postings create them.
/// </summary>
/// <remarks>
/// <para><b>Status mapping</b>: <c>duplicate_batch</c> -&gt; 409;
/// <c>batch_not_found</c> -&gt; 404; everything else (<c>batch_required</c>,
/// <c>batch_has_expired</c>, ...) describes a bad REQUEST -&gt; 400.</para>
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class BatchesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BatchesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>
    /// FEFO-ordered availability of one item in one warehouse (expiry nearest first, no-expiry
    /// last, zero balances excluded). The picking/issue UI source.
    /// </summary>
    /// <param name="itemId">Tracked item.</param>
    /// <param name="warehouseId">Warehouse to check.</param>
    /// <param name="asOf">Balance date (defaults to today UTC).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("availability")]
    [Authorize(Policy = "permission:batch:read")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] Guid itemId,
        [FromQuery] Guid warehouseId,
        [FromQuery] DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty || warehouseId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBatch"),
                _errors.Text(StockErrorCodes.BatchNotFound),
                StockErrorCodes.BatchNotFound);
        }

        var rows = await _sender.SendAsync(
            new GetBatchAvailabilityQuery(itemId, warehouseId, asOf), cancellationToken);
        return Ok(rows);
    }

    /// <summary>Registers one batch master manually (receipts auto-create otherwise).</summary>
    /// <param name="command">Item, number and optional dates.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:batch:write")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBatchCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                StockErrorCodes.DuplicateBatch => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("BatchConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("BatchRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(
            nameof(GetAvailability),
            new { itemId = dto.ItemId, warehouseId = Guid.Empty },
            dto);
    }

    /// <summary>Retires a batch (disable) or corrects its dates (number/item immutable).</summary>
    /// <param name="id">Batch ID.</param>
    /// <param name="command">Updated data including the same ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "permission:batch:write")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateBatchCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBatch"),
                _errors.Text(StockErrorCodes.BatchNotFound),
                StockErrorCodes.BatchNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                StockErrorCodes.BatchNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("BatchNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("BatchRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
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
