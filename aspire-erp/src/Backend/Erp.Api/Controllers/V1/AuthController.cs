using Erp.Application.Common;
using Erp.Application.Features.Security.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous] // Anyone can attempt to log in
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantProvider _tenantProvider;

    public AuthController(ISender sender, ITenantProvider tenantProvider)
    {
        _sender = sender;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant())
        {
            return BadRequest(new ProblemDetails
            {
                Type = "Auth.TenantRequired",
                Title = "Tenant resolution failed",
                Detail = "A valid X-Tenant-Id header is required to login."
            });
        }

        var command = new LoginCommand(
            Email: request.Email,
            Password: request.Password,
            TenantId: _tenantProvider.GetCurrentTenantId()
        );

        var result = await _sender.SendAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Unauthorized(new ProblemDetails
        {
            Type = result.Error?.Code ?? "Auth.Failed",
            Title = "Authentication Failed",
            Detail = result.Error?.Message ?? "Invalid credentials"
        });
    }

    /// <summary>
    /// Step 2 of the MFA login (module 16-auth-login-mfa, spec §3.2): redeems the
    /// password-proof <c>mfaTicket</c> returned by <c>login</c> when <c>isMfaRequired</c> is
    /// true, with a TOTP code (6 digits) or a backup code. Success returns the definitive
    /// JWT inside the standard login payload (<c>isMfaRequired</c> false); failures carry
    /// the spec's Domain Error Codes in the <c>code</c> extension
    /// (<c>AUTH_MFA_INVALID_CODE</c> -&gt; 401, <c>AUTH_ACCOUNT_LOCKED</c> -&gt; 423).
    /// </summary>
    [HttpPost("login-mfa")]
    public async Task<IActionResult> LoginMfa([FromBody] LoginMfaRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantProvider.HasTenant())
        {
            return BadRequest(new ProblemDetails
            {
                Type = "Auth.TenantRequired",
                Title = "Tenant resolution failed",
                Detail = "A valid X-Tenant-Id header is required to login."
            });
        }

        var result = await _sender.SendAsync(
            new VerifyLoginMfaCommand(
                MfaTicket: request.MfaTicket ?? string.Empty,
                MfaCode: request.MfaCode ?? string.Empty,
                TenantId: _tenantProvider.GetCurrentTenantId()),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;
        var status = error.Code == Erp.Domain.Entities.Security.AuthErrorCodes.AccountLocked
            ? StatusCodes.Status423Locked
            : StatusCodes.Status401Unauthorized;

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Authentication Failed",
            Status = status,
            Detail = error.Message,
            Instance = HttpContext.Request.Path.Value,
        };
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginMfaRequest
{
    public string? MfaTicket { get; set; }
    public string? MfaCode { get; set; }
}
