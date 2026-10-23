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

        // Deduplicate and collect roles
        var distinctRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (user.UserRoles != null)
        {
            foreach (var ur in user.UserRoles)
            {
                if (ur.Role?.Name != null)
                {
                    distinctRoles.Add(ur.Role.Name.Trim());
                }
            }
        }

        if (roles != null)
        {
            foreach (var r in roles)
            {
                if (!string.IsNullOrWhiteSpace(r))
                {
                    distinctRoles.Add(r.Trim());
                }
            }
        }

        foreach (var roleName in distinctRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
            claims.Add(new Claim("roles", roleName));
        }

        // Keep JWT payload lightweight to prevent HTTP 431 (Header Fields Too Large).
        // Granular authorization is evaluated server-side via DocTypePermissionAuthorizationHandler with IMemoryCache.
        var isSystemManager = distinctRoles.Contains("System Manager");
        if (isSystemManager)
        {
            claims.Add(new Claim("perms", "all"));
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
