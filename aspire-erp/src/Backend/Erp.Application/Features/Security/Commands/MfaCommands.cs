using Erp.Application.Common;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Stages MFA enrollment for the user (spec §3.2): generates a fresh TOTP shared secret and
/// the initial set of recovery codes. Enrollment is NOT active until
/// <see cref="VerifyMfaCommand"/> succeeds - the SPA must force the backup-codes download step
/// before calling verify.
/// </summary>
public sealed record EnableMfaCommand(Guid UserId, Guid TenantId)
    : ICommand<Result<EnableMfaResult>>;

/// <summary>Staged enrollment payload: QR material plus the one-time plaintext backup codes.</summary>
public sealed record EnableMfaResult(
    string SharedKey,
    string AuthenticatorUri,
    IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// Finalizes MFA enrollment by verifying a 6-digit TOTP code against the staged secret
/// (spec §3.2). Replay is bounded by construction: the code is valid for a single 30-second
/// step and enablement flips exactly once.
/// </summary>
public sealed record VerifyMfaCommand(Guid UserId, Guid TenantId, string Code)
    : ICommand<Result<Unit>>;

/// <summary>
/// Issues 10 fresh backup codes, INVALIDATING every previously issued code (spec §3.2).
/// Requires MFA to be enabled (<c>MFA_NOT_ENABLED</c> otherwise).
/// </summary>
public sealed record GenerateBackupCodesCommand(Guid UserId, Guid TenantId)
    : ICommand<Result<IReadOnlyList<string>>>;

/// <summary>
/// Turns MFA off after proving possession with a current TOTP code. Clears the staged secret
/// and every issued backup code.
/// </summary>
public sealed record DisableMfaCommand(Guid UserId, Guid TenantId, string Code)
    : ICommand<Result<Unit>>;
