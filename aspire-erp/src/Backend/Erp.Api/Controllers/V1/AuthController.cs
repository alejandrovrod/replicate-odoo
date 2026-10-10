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
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
