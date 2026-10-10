using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Infrastructure.Security;

public class JwtTokenGenerator : ITokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, IEnumerable<DocTypePermission> permissions, IEnumerable<string>? roles = null)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "AspireErpSuperSecretKeyThatIsAtLeast32BytesLongForHS256!!!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("tenant_id", user.TenantId.ToString())
        };

        foreach (var userRole in user.UserRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
            // Also inject the raw name for frontend
            claims.Add(new Claim("roles", userRole.Role.Name));
        }

        // Role names (short "roles" claim): lets the SPA resolve the System Manager
        // bypass and role badges without an extra round-trip. Authorization itself stays
        // server-side (DocTypePermission handler reads DB-backed perms claims).
        foreach (var role in roles ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim("roles", role.Trim()));
            }
        }

        // Flatten permissions into "perms" claims
        // Format: "doctype:action"
        // e.g. "sales_invoice:read"
        foreach (var p in permissions)
        {
            var dt = p.DocType.ToLowerInvariant().Replace(" ", "_");
            if (p.CanRead) claims.Add(new Claim("perms", $"{dt}:read"));
            if (p.CanWrite) claims.Add(new Claim("perms", $"{dt}:write"));
            if (p.CanCreate) claims.Add(new Claim("perms", $"{dt}:create"));
            if (p.CanDelete) claims.Add(new Claim("perms", $"{dt}:delete"));
            if (p.CanSubmit) claims.Add(new Claim("perms", $"{dt}:submit"));
            if (p.CanCancel) claims.Add(new Claim("perms", $"{dt}:cancel"));
        }

        // Token lifetime
        var expires = DateTime.UtcNow.AddHours(24);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = credentials,
            Issuer = "AspireErp",
            Audience = "AspireErp"
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return handler.WriteToken(token);
    }
}
