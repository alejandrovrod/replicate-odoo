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
/// Serial master endpoints (batch-and-serial Fase 3): availability reads and single-serial
/// verification. Route resolves to <c>/api/v1/serialnumbers</c>. Serials are born in
/// movements (receipts create, issues deliver) - there is no manual create endpoint, so a
/// number can never exist without stock behind it.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class SerialNumbersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SerialNumbersController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Active serials of one item in one warehouse (picker/verification source).</summary>
    /// <param name="itemId">Tracked item.</param>
    /// <param name="warehouseId">Warehouse to check.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:serial_number:read")]
    [ProducesResponseType(typeof(IReadOnlyList<SerialNoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid itemId,
        [FromQuery] Guid warehouseId,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty || warehouseId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidSerial"),
                _errors.Text(StockErrorCodes.SerialNotFound),
                StockErrorCodes.SerialNotFound);
        }

        var rows = await _sender.SendAsync(new GetSerialsQuery(itemId, warehouseId), cancellationToken);
        return Ok(rows);
    }

    /// <summary>Verifies one serial number (status + location), or 404 when unknown.</summary>
    /// <param name="number">Serial number to verify.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("verify")]
    [Authorize(Policy = "permission:serial_number:read")]
    [ProducesResponseType(typeof(SerialNoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(
        [FromQuery] string number,
        CancellationToken cancellationToken)
    {
        var serial = await _sender.SendAsync(new VerifySerialQuery(number), cancellationToken);

        return serial is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("SerialNotFound"),
                _errors.Text(StockErrorCodes.SerialNotFound),
                StockErrorCodes.SerialNotFound)
            : Ok(serial);
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
