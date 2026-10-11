namespace Erp.Domain.Entities.Security;

/// <summary>
/// Tunables for the User Profile &amp; Security module (spec 15-user-profile §3). Kept next to
/// the guarded entities so handlers and tests share one source of truth.
/// </summary>
public static class ProfileRules
{
    /// <summary>Minimum length for a new password (spec §3.1: MinLength 12).</summary>
    public const int MinPasswordLength = 12;

    /// <summary>Failed password verifications before the account locks (plan Phase 1.4).</summary>
    public const int MaxFailedAccessAttempts = 5;

    /// <summary>How long a brute-force lockout lasts.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>How many backup codes a generation issues (spec §3.2: 10).</summary>
    public const int RecoveryCodeCount = 10;
}
