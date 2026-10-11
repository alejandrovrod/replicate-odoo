using Erp.Domain.Common;

namespace Erp.Domain.Entities.Security;

/// <summary>
/// One single-use MFA backup code (module 15-user-profile, spec §3.2). Only the SHA-256 hash
/// is persisted - the plaintext is shown to the user exactly once at generation time.
/// Regenerating codes deletes every existing row for the user, invalidating previously
/// issued codes by construction.
/// </summary>
public class UserRecoveryCode : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hex of the normalized (uppercase, no-separator) plaintext code.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the code is redeemed; a redeemed code can never be reused.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
