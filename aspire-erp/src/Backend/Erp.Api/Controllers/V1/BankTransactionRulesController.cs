using Erp.Api.Common;
using Erp.Api.Localization;
using Erp.Api.Shared;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Queries;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Heuristic rule management endpoints (task 6.3, Block C): list and create the matching
/// rules the rule-run evaluates. Attributes follow Constitution Article VI: explicit route +
/// versioning + TenantMember policy (VI.1), JSON content negotiation (VI.2) and exhaustive
/// status documentation (VI.3).
/// </summary>
[ApiController]
[Route("api/v1/bank-transaction-rules")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class BankTransactionRulesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IStringLocalizer<ErrorMessages> _errors;
    private readonly IStringLocalizer<CommonMessages> _common;

    public BankTransactionRulesController(
        ISender sender,
        IStringLocalizer<ErrorMessages> errors,
        IStringLocalizer<CommonMessages> common)
    {
        _sender = sender;
        _errors = errors;
        _common = common;
    }

    /// <summary>Returns every heuristic rule of the company (active and inactive).</summary>
    [HttpGet]
    [Authorize(Policy = "permission:bank_transaction_rule:read")]
    [ProducesResponseType(typeof(PagedResult<BankTransactionRuleDto>), StatusCodes.Status200OK)]
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
                _errors.Text(BankingErrorCodes.BankTransactionRuleNotFound),
                BankingErrorCodes.BankTransactionRuleNotFound);
        }

        var rules = await _sender.SendAsync(
            new GetBankTransactionRulesQuery(companyId, page, pageSize), cancellationToken);
        return Ok(rules);
    }

    /// <summary>Creates one heuristic matching rule (201 + the persisted rule).</summary>
    [HttpPost]
    [Authorize(Policy = "permission:bank_transaction_rule:write")]
    [ProducesResponseType(typeof(BankTransactionRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankTransactionRuleCommand command,
        CancellationToken cancellationToken)
    {
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
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    _common.Text("RuleRejected"),
                    _errors.Text(error.Code, error.Message),
                    error.Code),
            };
        }

        var rule = result.Value!;
        return CreatedAtAction(nameof(Get), new { companyId = rule.CompanyId }, rule);
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
