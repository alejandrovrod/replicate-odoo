using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Auth.Commands;

public record LoginCommand(string Email, string Password, string? MfaCode = null) : IRequest<LoginResult>;

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, string? TenantSlug, string? TenantName, List<string> Roles, List<string> Permissions);

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;

    public LoginCommandHandler(UserManager<ApplicationUser> userManager, IJwtTokenGenerator jwtTokenGenerator, ISecurityDbContext securityDb, IPlatformDbContext platformDb)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _securityDb = securityDb;
        _platformDb = platformDb;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Tu cuenta está desactivada. Contactá a un administrador.");
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(request.MfaCode))
            {
                throw new UnauthorizedAccessException("MFA_REQUIRED");
            }

            var secretToken = await _securityDb.UserTokens
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.LoginProvider == "Authenticator" && t.Name == "Secret", cancellationToken);

            if (secretToken == null || string.IsNullOrEmpty(secretToken.Value))
            {
                throw new InvalidOperationException("La configuración MFA del usuario está corrupta o incompleta.");
            }

            var keyBytes = Base32.Base32Encoder.Decode(secretToken.Value);
            var totp = new OtpNet.Totp(keyBytes, step: 30, totpSize: 6);

            if (!totp.VerifyTotp(request.MfaCode, out _, OtpNet.VerificationWindow.RfcSpecifiedNetworkDelay))
            {
                // Verify recovery codes if TOTP fails
                var recoveryTokens = await _securityDb.UserTokens
                    .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes")
                    .ToListAsync(cancellationToken);

                var usedRecoveryToken = recoveryTokens.FirstOrDefault(t => t.Value == request.MfaCode);
                if (usedRecoveryToken != null)
                {
                    // Code matches a recovery code. Consume it.
                    _securityDb.UserTokens.Remove(usedRecoveryToken);
                    await _securityDb.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    throw new UnauthorizedAccessException("Código MFA inválido");
                }
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Si el usuario pertenece a un tenant, la matriz de permisos es la
        // del tenant (RolePermissions.TenantId); usuarios sin tenant (system
        // admin) reciben todos los permisos globales.
        var permissions = await ResolvePermissionsAsync(user, roles, cancellationToken);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles, permissions);
        var refreshTokenStr = _jwtTokenGenerator.GenerateRefreshToken();

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenStr, // Simple storage for now, ideally hashed
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            FamilyId = Guid.NewGuid()
        };

        _securityDb.RefreshTokens.Add(rt);
        await _securityDb.SaveChangesAsync(cancellationToken);

        string? tenantSlug = null;
        string? tenantName = null;

        if (user.TenantId.HasValue)
        {
            var tenant = await _platformDb.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, cancellationToken);
            if (tenant != null)
            {
                tenantSlug = tenant.Slug;
                tenantName = tenant.Name;
            }
        }

        return new LoginResult(accessToken, refreshTokenStr, 15 * 60, tenantSlug, tenantName, roles.ToList(), permissions);
    }

    internal async Task<List<string>> ResolvePermissionsAsync(ApplicationUser user, IList<string> roles, CancellationToken cancellationToken)
    {
        var roleIds = await _securityDb.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0) return new List<string>();

        var query = _securityDb.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId));

        if (user.TenantId.HasValue)
        {
            query = query.Where(rp => rp.TenantId == user.TenantId.Value);
        }

        var permissionIds = await query
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _securityDb.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
    }
}
