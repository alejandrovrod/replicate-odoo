using Erp.Application.Common;
using Erp.Application.Features.ExchangeRates.Commands;
using Erp.Application.Features.ExchangeRates.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/exchange-rate-revaluations")]
public class ExchangeRateRevaluationsController : ControllerBase
{
    private readonly ISender _sender;

    public ExchangeRateRevaluationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = "permission:exchange_rate_revaluation:read")]
    public async Task<IActionResult> List([FromQuery] Guid companyId, [FromQuery] string? status)
    {
        var result = await _sender.SendAsync(new GetExchangeRateRevaluationsQuery(companyId, status));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:exchange_rate_revaluation:read")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var result = await _sender.SendAsync(new GetExchangeRateRevaluationDetailQuery(id));
        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = result.Error!.Message, Type = result.Error!.Code });
        }
        return Ok(result.Value);
    }

    [HttpGet("preview")]
    [Authorize(Policy = "permission:exchange_rate_revaluation:read")]
    public async Task<IActionResult> Preview([FromQuery] Guid companyId, [FromQuery] DateOnly postingDate, [FromQuery] decimal allowance, [FromQuery] Guid? fxAccountId)
    {
        var result = await _sender.SendAsync(new GetRevaluationPreviewQuery(companyId, postingDate, allowance, fxAccountId));
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "permission:exchange_rate_revaluation:write")]
    public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string idempotencyKey, [FromBody] CreateExchangeRateRevaluationCommand command)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Idempotency-Key header is required.", Type = "idempotency_key_required" });

        var cmd = command with { IdempotencyKey = idempotencyKey };
        var result = await _sender.SendAsync(cmd);
        if (!result.IsSuccess)
            return MapError(result.Error!);

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "permission:exchange_rate_revaluation:submit")]
    public async Task<IActionResult> Submit(Guid id, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, [FromBody] SubmitExchangeRateRevaluationCommand command)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Idempotency-Key header is required.", Type = "idempotency_key_required" });

        var cmd = command with { RevaluationId = id, IdempotencyKey = idempotencyKey };
        var result = await _sender.SendAsync(cmd);
        if (!result.IsSuccess)
            return MapError(result.Error!);

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "permission:exchange_rate_revaluation:cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, [FromBody] CancelExchangeRateRevaluationCommand command)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Idempotency-Key header is required.", Type = "idempotency_key_required" });

        var cmd = command with { RevaluationId = id, IdempotencyKey = idempotencyKey };
        var result = await _sender.SendAsync(cmd);
        if (!result.IsSuccess)
            return MapError(result.Error!);

        return Ok(result.Value);
    }

    private IActionResult MapError(Error error)
    {
        var type = error.Code;
        if (type == "exchange_rate_invalid" || type == "exchange_rate_self_pair" || type == "unknown_currency" || type == "invalid_exchange_gain_loss_account")
        {
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = error.Message, Type = type });
        }
        if (type == "exchange_rate_missing" || type == "payment_currency_mismatch" || type == "no_revaluation_gain_loss" || type == "revaluation_dust_above_allowance" || type == "revaluation_future_date")
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Unprocessable Entity", Detail = error.Message, Type = type });
        }
        if (type == "revaluation_not_found")
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = error.Message, Type = type });
        }
        if (type == "revaluation_invalid_transition" || type == "revaluation_already_cancelled" || type == "fiscal_year_closed" || type == "fiscal_period_locked" || type == "concurrency_conflict")
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = error.Message, Type = type });
        }
        if (type == "double_entry_imbalance")
        {
            return StatusCode(500, new ProblemDetails { Title = "Internal Server Error", Detail = error.Message, Type = type });
        }
        
        return BadRequest(new ProblemDetails { Title = "Error", Detail = error.Message, Type = type });
    }
}
