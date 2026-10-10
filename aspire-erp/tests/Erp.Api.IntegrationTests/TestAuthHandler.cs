using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Api.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    /// <summary>
    /// Opt-out of authentication: the handler returns NoResult so authorization policies
    /// fail with 401 - proves anonymous callers cannot reach protected endpoints.
    /// </summary>
    public const string NoAuthHeader = "X-Test-NoAuth";

    /// <summary>
    /// Comma-separated role names for this request (default: System Manager bypass, so every
    /// pre-existing suite keeps passing without changes).
    /// </summary>
    public const string RolesHeader = "X-Test-Roles";

    /// <summary>
    /// Comma-separated <c>doctype:action</c> codes for this request (the flattened JWT
    /// <c>perms</c> shape, e.g. <c>customer:read,customer:write</c>).
    /// </summary>
    public const string PermsHeader = "X-Test-Perms";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(NoAuthHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = Request.Headers.TryGetValue(RolesHeader, out var roleValues)
            ? roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : ["System Manager"];

        var perms = Request.Headers.TryGetValue(PermsHeader, out var permValues)
            ? permValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        // En los tests inyectamos por defecto el rol de System Manager
        // Para que todos los permisos de DocTypes pasen de largo (bypass).
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var perm in perms)
        {
            claims.Add(new Claim("perms", perm));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
