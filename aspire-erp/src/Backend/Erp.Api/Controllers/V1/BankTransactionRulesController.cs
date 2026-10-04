using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Banking.Commands;
using Erp.Application.Features.Banking.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public BankTransactionRulesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns every heuristic rule of the company (active and inactive).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BankTransactionRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.",
                BankingErrorCodes.BankTransactionRuleNotFound);
        }

        var rules = await _sender.SendAsync(
            new GetBankTransactionRulesQuery(companyId), cancellationToken);
        return Ok(rules);
    }

    /// <summary>Creates one heuristic matching rule (201 + the persisted rule).</summary>
    [HttpPost]
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
                    "Bank Account Not Found",
                    error.Message,
                    error.Code),
                _ => Problem(
                    StatusCodes.Status400BadRequest,
                    "Rule Rejected",
                    error.Message,
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
