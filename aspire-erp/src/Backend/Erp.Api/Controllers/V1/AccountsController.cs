using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Application.Features.Accounts.Commands;
using Erp.Application.Features.Accounts.Queries;
using Erp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

/// <summary>
/// Chart of Accounts (COA) endpoints (Tasks 2.3). Every attribute below is mandated by
/// Constitution Article VI: explicit route + versioning + TenantMember policy (VI.1),
/// content negotiation (VI.2) and exhaustive status documentation (VI.3).
/// </summary>
/// <remarks>
/// No [IdempotencyKeyRequired] here: Article VI.4 scopes that guard to ledger-posting mutations,
/// and account creation is not a ledger posting. Tenant scoping itself is enforced by
/// TenantResolutionMiddleware (X-Tenant-ID) + AppDbContext's global query filter.
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "TenantMember")]
[Produces("application/json")]
[Consumes("application/json")]
public sealed class AccountsController : ControllerBase
{
    private readonly ISender _sender;

    public AccountsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the company's Chart of Accounts as a nested tree (roots at the top).</summary>
    /// <param name="companyId">Company that owns the COA.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<AccountTreeNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTree([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Problem400(
                "Invalid Company",
                "The companyId query parameter must be a non-empty GUID.");
        }

        var tree = await _sender.SendAsync(new GetAccountTreeQuery(companyId), cancellationToken);
        return Ok(tree);
    }

    /// <summary>Creates one account (root level or under a Group parent). 201 with the created account.</summary>
    /// <param name="command">Account data.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Error!;

            // RFC 7807: duplicate code is a conflict, every other domain failure is a bad request.
            return error.Code switch
            {
                AccountErrorCodes.DuplicateAccountCode => Problem(
                    StatusCodes.Status409Conflict,
                    "Duplicate Account Code",
                    error.Message,
                    error.Code),
                _ => Problem400("Account Validation Failed", error.Message, error.Code),
            };
        }

        var dto = result.Value!;
        return CreatedAtAction(nameof(GetTree), new { companyId = dto.CompanyId }, dto);
    }

    private ObjectResult Problem400(string title, string detail, string? code = null)
        => Problem(StatusCodes.Status400BadRequest, title, detail, code);

    private ObjectResult Problem(int status, string title, string detail, string? code)
    {
        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
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
