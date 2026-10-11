using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Infrastructure.Security;

/// <summary>
/// JWT-backed MFA challenge tickets (spec 16 §3.1). Same HMAC secret as the session tokens
/// (<c>JwtSettings:Secret</c>) but a disjoint purpose claim and a 5-minute lifetime, so a
/// ticket can never authenticate as a session token and vice versa: consumers must check
/// <c>purpose == "mfa-challenge"</c> via <see cref="TryValidateTicket"/> (the ASP.NET JWT
/// bearer pipeline is NOT involved - this is an opaque-to-clients command input).
/// </summary>
public sealed class MfaChallengeTokenService : IMfaChallengeTokenService
{
    public const string Purpose = "mfa-challenge";

    /// <summary>Password-proven challenges expire fast: enough to type 1-2 TOTP windows.</summary>
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);

    private readonly IConfiguration _configuration;

    public MfaChallengeTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateTicket(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("tenant_id", user.TenantId.ToString()),
                new Claim("purpose", Purpose),
            }),
            Expires = DateTime.UtcNow.Add(TicketLifetime),
            SigningCredentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256),
            Issuer = "AspireErp",
            Audience = "AspireErp",
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public bool TryValidateTicket(string? ticket, out Guid userId, out Guid tenantId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        var handler = new JwtSecurityTokenHandler();
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(
                ticket,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "AspireErp",
                    ValidAudience = "AspireErp",
                    IssuerSigningKey = SigningKey(),
                    ClockSkew = TimeSpan.Zero,
                },
                out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return false;
        }

        if (!string.Equals(
                principal.FindFirst("purpose")?.Value, Purpose, StringComparison.Ordinal)
            || !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId)
            || userId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId)
            || tenantId == Guid.Empty)
        {
            userId = Guid.Empty;
            tenantId = Guid.Empty;
            return false;
        }

        return true;
    }

    private SymmetricSecurityKey SigningKey()
    {
        var secret = _configuration["JwtSettings:Secret"]
            ?? "AspireErpSuperSecretKeyThatIsAtLeast32BytesLongForHS256!!!";
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}
