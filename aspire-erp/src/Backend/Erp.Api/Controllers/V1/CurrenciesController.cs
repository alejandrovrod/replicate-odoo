using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Currencies.Commands;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Global ISO currency catalog endpoints (RM-09). Currencies are shared across tenants, so the
/// list takes no company scope. No <c>[IdempotencyKeyRequired]</c> here: Article VI.4 scopes that
/// guard to ledger-posting mutations and maintaining the catalog writes no GLEntry row.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class CurrenciesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrencyRepository _currencies;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public CurrenciesController(
        ISender sender,
        ICurrencyRepository currencies,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _currencies = currencies;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the global currency catalog ordered by code.</summary>
    /// <param name="onlyActive">When true (default), only active currencies.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:currency:read")]
    [ProducesResponseType(typeof(IReadOnlyList<CurrencyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] bool onlyActive = true,
        CancellationToken cancellationToken = default)
    {
        var currencies = await _currencies.GetAllAsync(onlyActive, cancellationToken);
        return Ok(currencies.Select(CurrencyDto.From).ToList());
    }

    /// <summary>Returns one currency by id.</summary>
    /// <param name="id">Currency id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:currency:read")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var currency = await _currencies.GetByIdAsync(id, cancellationToken);
        return currency is null
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("CurrencyNotFound"),
                _errors.Text(CurrencyErrorCodes.CurrencyNotFound),
                CurrencyErrorCodes.CurrencyNotFound)
            : Ok(CurrencyDto.From(currency));
    }

    /// <summary>Creates one currency (codes are unique globally - duplicates are a 409).</summary>
    /// <param name="command">Currency data (code, symbol, fraction name).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:currency:write")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCurrencyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                CurrencyErrorCodes.DuplicateCurrencyCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("DuplicateCurrencyCode"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("CurrencyRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Updates an existing currency. 200 with the updated currency.</summary>
    /// <param name="id">Currency ID.</param>
    /// <param name="command">Updated currency data including RowVersion.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "permission:currency:write")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCurrencyCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidCurrencyId"),
                _errors.Text(CurrencyErrorCodes.CurrencyNotFound),
                CurrencyErrorCodes.CurrencyNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                CurrencyErrorCodes.CurrencyNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("CurrencyNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict or CurrencyErrorCodes.DuplicateCurrencyCode => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("CurrencyConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("CurrencyRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Disables a currency (sets IsActive = false).</summary>
    [HttpPut("{id}/disable")]
    [Authorize(Policy = "permission:currency:write")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, false, cancellationToken);

    /// <summary>Enables a currency (sets IsActive = true).</summary>
    [HttpPut("{id}/enable")]
    [Authorize(Policy = "permission:currency:write")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var currency = await _currencies.GetByIdAsync(id, cancellationToken);
        if (currency is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("CurrencyNotFound"),
                _errors.Text(CurrencyErrorCodes.CurrencyNotFound),
                CurrencyErrorCodes.CurrencyNotFound);
        }

        currency.IsActive = isActive;
        await _currencies.UpdateAsync(currency, cancellationToken);
        return Ok(CurrencyDto.From(currency));
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
