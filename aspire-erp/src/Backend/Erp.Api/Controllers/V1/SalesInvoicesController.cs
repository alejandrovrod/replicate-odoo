using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/sales-invoices")]
public class SalesInvoicesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public SalesInvoicesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    [HttpPost("pos")]
    [ProducesResponseType(typeof(SalesInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SalesInvoiceDto>> SubmitPOSInvoice(
        [FromBody] SubmitPOSInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            // Same RFC 7807 shape as every other rejection: the previous
            // `BadRequest(new { result.Error })` body was unreadable by the shared
            // `apiClient` interceptor, so its message never reached the UI.
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = _common.Text("SalesInvoiceRejected"),
                Status = StatusCodes.Status400BadRequest,
                Detail = _errors.Text(error.Code, error.Message),
                Instance = HttpContext.Request.Path.Value,
            };
            problem.Extensions["code"] = error.Code;
            return BadRequest(problem);
        }

        return Ok(result.Value);
    }
}
