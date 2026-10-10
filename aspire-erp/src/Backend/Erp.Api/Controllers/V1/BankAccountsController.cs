using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Commands;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Bank account master endpoints: the company-owned profiles that link an external account
/// number to a postable GL account (statement imports and reconciliations resolve through
/// them). No <c>[IdempotencyKeyRequired]</c> here: Article VI.4 scopes that guard to
/// ledger-posting mutations and maintaining the master writes no GLEntry row.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class BankAccountsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IBankRepository _banks;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BankAccountsController(
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

    /// <summary>Returns the company's bank accounts.</summary>
    /// <param name="companyId">Company that owns the accounts.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Page size, 1..500 (default 50).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [Authorize(Policy = "permission:bank_account:read")]
    [ProducesResponseType(typeof(PagedResult<BankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
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
                _errors.Text(BankingErrorCodes.CompanyNotFound),
                BankingErrorCodes.CompanyNotFound);
        }

        var result = await _banks.GetAccountsPagedAsync(companyId, new PagedRequest(page, pageSize), cancellationToken);
        return Ok(new PagedResult<BankAccountDto>(
            result.Items.Select(BankAccountDto.From).ToList(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize));
    }

    /// <summary>Returns one bank account by id.</summary>
    /// <param name="id">Bank account id.</param>
    /// <param name="companyId">Company that owns the account.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:bank_account:read")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBankAccount"),
                _errors.Text(BankingErrorCodes.BankAccountNotFound),
                BankingErrorCodes.BankAccountNotFound);
        }

        var account = await _banks.GetAccountByIdAsync(id, cancellationToken);
        return account is null || account.CompanyId != companyId
            ? Problem(
                StatusCodes.Status404NotFound,
                _common.Text("BankAccountNotFound"),
                _errors.Text(BankingErrorCodes.BankAccountNotFound),
                BankingErrorCodes.BankAccountNotFound)
            : Ok(BankAccountDto.From(account));
    }

    /// <summary>Creates one bank account master row.</summary>
    /// <param name="command">Bank account data (company, names, number, GL account, currency).</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [Authorize(Policy = "permission:bank_account:write")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("BankAccountRejected"),
                _errors.Text(error.Code, error.Message),
                error.Code);
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetById), new { id = dto.Id, companyId = dto.CompanyId }, dto);
    }

    /// <summary>Updates an existing bank account. 200 with the updated account.</summary>
    /// <param name="id">Bank account ID.</param>
    /// <param name="command">Updated bank account data including RowVersion.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id}")]
    [Authorize(Policy = "permission:bank_account:write")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBankAccountCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                _common.Text("InvalidBankAccount"),
                _errors.Text(BankingErrorCodes.BankAccountNotFound),
                BankingErrorCodes.BankAccountNotFound);
        }

        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;
            return error.Code switch
            {
                BankingErrorCodes.BankAccountNotFound => Problem(
                    StatusCodes.Status404NotFound,
                    _common.Text("BankAccountNotFound"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
                ConcurrencyErrorCodes.ConcurrencyConflict => Problem(
                    StatusCodes.Status409Conflict,
                    _common.Text("BankAccountConflict"),
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("BankAccountRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        return Ok(result.Value!);
    }

    /// <summary>Disables a bank account (sets IsActive = false).</summary>
    [HttpPut("{id}/disable")]
    [Authorize(Policy = "permission:bank_account:write")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, false, cancellationToken);

    /// <summary>Enables a bank account (sets IsActive = true).</summary>
    [HttpPut("{id}/enable")]
    [Authorize(Policy = "permission:bank_account:write")]
    [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
        => await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var account = await _banks.GetAccountByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                _common.Text("BankAccountNotFound"),
                _errors.Text(BankingErrorCodes.BankAccountNotFound),
                BankingErrorCodes.BankAccountNotFound);
        }

        account.IsActive = isActive;
        await _banks.UpdateAccountAsync(account, cancellationToken);
        return Ok(BankAccountDto.From(account));
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
