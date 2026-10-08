using Erp.Application.Common;
using Erp.Application.Features.ExchangeRates.Commands;
using Erp.Application.Features.ExchangeRates.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly ISender _sender;

    public ExchangeRatesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid from, [FromQuery] Guid to, [FromQuery] DateOnly date)
    {
        var result = await _sender.SendAsync(new GetExchangeRateQuery(from, to, date));
        if (!result.IsSuccess)
        {
            return result.Error!.Code == "exchange_rate_missing" 
                ? UnprocessableEntity(new ProblemDetails { Title = "Unprocessable Entity", Detail = result.Error!.Message, Type = result.Error!.Code })
                : BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error!.Message, Type = result.Error!.Code });
        }
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertExchangeRateCommand command)
    {
        var result = await _sender.SendAsync(command);
        if (!result.IsSuccess)
        {
            if (result.Error!.Code == "concurrency_conflict")
                return Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error!.Message, Type = result.Error!.Code });
                
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error!.Message, Type = result.Error!.Code });
        }
        return Ok(result.Value);
    }
}
