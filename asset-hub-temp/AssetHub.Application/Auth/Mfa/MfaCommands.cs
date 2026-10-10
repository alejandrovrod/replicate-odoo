using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using Base32;

namespace AssetHub.Application.Auth.Mfa;

public record GetMfaStatusQuery() : IRequest<MfaStatusDto>;

public class MfaStatusDto
{
    public bool IsEnabled { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, MfaStatusDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public GetMfaStatusQueryHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<MfaStatusDto> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        // Count recovery codes from UserTokens
        var recoveryCodes = await _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes")
            .Select(t => t.Value)
            .ToListAsync(cancellationToken);

        return new MfaStatusDto
        {
            IsEnabled = user.TwoFactorEnabled,
            RecoveryCodesRemaining = recoveryCodes.Count
        };
    }
}

public record InitiateMfaSetupCommand() : IRequest<MfaSetupDto>;

public class MfaSetupDto
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public string[] BackupCodes { get; set; } = Array.Empty<string>();
}

public class InitiateMfaSetupCommandHandler : IRequestHandler<InitiateMfaSetupCommand, MfaSetupDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public InitiateMfaSetupCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<MfaSetupDto> Handle(InitiateMfaSetupCommand request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("MFA ya está habilitado. Deshabilítalo primero si quieres reconfigurarlo.");
        }

        // Generate secret key (160 bits = 20 bytes = 32 base32 chars)
        var keyBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoder.Encode(keyBytes);

        // Generate QR code URI
        var issuer = "AssetHub";
        var account = user.Email ?? user.UserName ?? "user";
        var qrCodeUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";

        // Generate backup codes (10 codes, 8 chars each)
        var backupCodes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var codeBytes = KeyGeneration.GenerateRandomKey(4); // 4 bytes = 8 hex chars
            backupCodes.Add(BitConverter.ToString(codeBytes).Replace("-", "").ToLower().Substring(0, 8));
        }

        // Remove any existing setup tokens to avoid duplicate key exceptions
        var existingSetupTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "MfaSetup");
        _securityDb.UserTokens.RemoveRange(existingSetupTokens);

        // Store secret and backup codes temporarily (not enabled yet)
        // We'll store them in UserTokens
        _securityDb.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "MfaSetup",
            Name = "Secret",
            Value = secret
        });
        _securityDb.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "MfaSetup",
            Name = "BackupCodes",
            Value = string.Join(",", backupCodes)
        });

        await _securityDb.SaveChangesAsync(cancellationToken);

        return new MfaSetupDto
        {
            Secret = secret,
            QrCodeUri = qrCodeUri,
            BackupCodes = backupCodes.ToArray()
        };
    }
}

public record VerifyMfaSetupCommand(string TotpCode) : IRequest<Unit>;

public class VerifyMfaSetupCommandHandler : IRequestHandler<VerifyMfaSetupCommand, Unit>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public VerifyMfaSetupCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(VerifyMfaSetupCommand request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        // Get stored secret
        var secretToken = await _securityDb.UserTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.LoginProvider == "MfaSetup" && t.Name == "Secret", cancellationToken);
        
        if (secretToken == null || string.IsNullOrEmpty(secretToken.Value))
        {
            throw new InvalidOperationException("No hay configuración MFA pendiente. Inicia la configuración primero.");
        }

        var secret = secretToken.Value;

        // Verify TOTP code
        var keyBytes = Base32Encoder.Decode(secret);
        var totp = new Totp(keyBytes, step: 30, totpSize: 6);
        
        if (!totp.VerifyTotp(request.TotpCode, out _, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            throw new UnauthorizedAccessException("Código TOTP inválido");
        }

        // Get backup codes
        var backupCodesToken = await _securityDb.UserTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.LoginProvider == "MfaSetup" && t.Name == "BackupCodes", cancellationToken);
        
        var backupCodesStr = backupCodesToken?.Value;
        var backupCodes = backupCodesStr?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

        // Enable MFA
        user.TwoFactorEnabled = true;
        
        // Remove any existing recovery codes
        var existingRecoveryTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes");
        _securityDb.UserTokens.RemoveRange(existingRecoveryTokens);

        // Store new backup codes
        for (int i = 0; i < backupCodes.Count; i++)
        {
            _securityDb.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = user.Id,
                LoginProvider = "RecoveryCodes",
                Name = $"code{i}",
                Value = backupCodes[i]
            });
        }

        // Clear setup tokens and save permanent secret
        var setupTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "MfaSetup");
        _securityDb.UserTokens.RemoveRange(setupTokens);

        _securityDb.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "Authenticator",
            Name = "Secret",
            Value = secret
        });

        await _userManager.UpdateAsync(user);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public record DisableMfaCommand(string Password) : IRequest<Unit>;

public class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand, Unit>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public DisableMfaCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        if (!user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("MFA no está habilitado");
        }

        // Verify password
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Contraseña incorrecta");
        }

        // Disable MFA
        user.TwoFactorEnabled = false;

        // Remove all recovery codes
        var recoveryTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes");
        _securityDb.UserTokens.RemoveRange(recoveryTokens);

        // Remove any setup or authenticator tokens
        var setupTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && (t.LoginProvider == "MfaSetup" || t.LoginProvider == "Authenticator"));
        _securityDb.UserTokens.RemoveRange(setupTokens);

        await _userManager.UpdateAsync(user);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public record GetBackupCodesCommand() : IRequest<BackupCodesDto>;

public class BackupCodesDto
{
    public string[] Codes { get; set; } = Array.Empty<string>();
}

public class GetBackupCodesCommandHandler : IRequestHandler<GetBackupCodesCommand, BackupCodesDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public GetBackupCodesCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<BackupCodesDto> Handle(GetBackupCodesCommand request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        var recoveryCodes = await _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes")
            .Select(t => t.Value)
            .ToListAsync(cancellationToken);

        return new BackupCodesDto
        {
            Codes = recoveryCodes.Where(c => c != null).Cast<string>().ToArray()
        };
    }
}

public record RegenerateBackupCodesCommand() : IRequest<BackupCodesDto>;

public class RegenerateBackupCodesCommandHandler : IRequestHandler<RegenerateBackupCodesCommand, BackupCodesDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public RegenerateBackupCodesCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<BackupCodesDto> Handle(RegenerateBackupCodesCommand request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        if (!user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("MFA no está habilitado");
        }

        // Generate new backup codes
        var backupCodes = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var codeBytes = KeyGeneration.GenerateRandomKey(4);
            backupCodes.Add(BitConverter.ToString(codeBytes).Replace("-", "").ToLower().Substring(0, 8));
        }

        // Remove old recovery codes
        var recoveryTokens = _securityDb.UserTokens
            .Where(t => t.UserId == user.Id && t.LoginProvider == "RecoveryCodes");
        _securityDb.UserTokens.RemoveRange(recoveryTokens);

        // Store new backup codes
        for (int i = 0; i < backupCodes.Count; i++)
        {
            _securityDb.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = user.Id,
                LoginProvider = "RecoveryCodes",
                Name = $"code{i}",
                Value = backupCodes[i]
            });
        }

        await _userManager.UpdateAsync(user);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return new BackupCodesDto
        {
            Codes = backupCodes.ToArray()
        };
    }
}