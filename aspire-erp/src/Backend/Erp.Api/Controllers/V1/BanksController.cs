using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Queries;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Offline master bank catalog endpoints (bank-catalog spec §4): country-filtered autocomplete
/// over the seeded catalog. No idempotency guard here (Article VI.4 scopes it to ledger-posting
/// mutations; these are reads).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class BanksController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IBankRepository _banks;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BanksController(
        ISender sender,
        IBankRepository banks,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _banks = banks;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns the active catalog banks of a country (autocomplete read).</summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country (default "MX").</param>
    /// <param name="country">Alias for <paramref name="countryCode"/> (spec shorthand ?country=MX).</param>
    /// <param name="search">Optional code/name filter (e.g. "012" autocompletes BBVA México).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:bank_account:read")]
    [ProducesResponseType(typeof(IReadOnlyList<BankDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string? countryCode = null,
        [FromQuery] string? country = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.SendAsync(
            new GetBanksQuery(countryCode ?? country, search), cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBankCountry"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        return Ok(result.Value!);
    }

    /// <summary>Returns one catalog bank by id.</summary>
    /// <param name="id">Catalog bank id.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:bank_account:read")]
    [ProducesResponseType(typeof(BankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var bank = await _banks.GetBankByIdAsync(id, cancellationToken);
        return bank is null || !bank.IsActive
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("BankNotFound"),
                _errors.Text(BankingErrorCodes.BankNotFound),
                BankingErrorCodes.BankNotFound)
            : Ok(BankDto.From(bank));
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
