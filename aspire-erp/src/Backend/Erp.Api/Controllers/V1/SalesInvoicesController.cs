using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Selling.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/sales-invoices")]
public class SalesInvoicesController : ControllerBase
{
    private readonly ISender _sender;

    public SalesInvoicesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("pos")]
    public async Task<ActionResult<SalesInvoiceDto>> SubmitPOSInvoice(
        [FromBody] SubmitPOSInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { result.Error });
        }

        return Ok(result.Value);
    }
}
