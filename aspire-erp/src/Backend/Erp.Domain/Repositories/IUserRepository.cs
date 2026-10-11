using Erp.Domain.Entities.Security;

namespace Erp.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<DocTypePermission>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user with the given id in the CURRENT tenant (global query filter), or null when it
    /// does not exist here. Recovery codes are NOT loaded - use
    /// <see cref="GetActiveRecoveryCodesAsync"/> when they are needed.
    /// </summary>
    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persists mutations on a tracked user (password, lockout, MFA fields).</summary>
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Unredeemed recovery codes of the user in the current tenant, oldest first.</summary>
    Task<IReadOnlyList<UserRecoveryCode>> GetActiveRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the user's recovery codes: deletes every existing row (invalidating
    /// previously issued codes by construction) and inserts <paramref name="codes"/>.
    /// An empty list clears all codes (used by MFA disable).
    /// </summary>
    Task ReplaceRecoveryCodesAsync(User user, IReadOnlyList<UserRecoveryCode> codes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one backup code as redeemed (<c>UsedAt = UtcNow</c>). Backup codes are single-use:
    /// a redeemed code can never authenticate again (spec 16 §3.2).
    /// </summary>
    Task MarkRecoveryCodeUsedAsync(Guid codeId, CancellationToken cancellationToken = default);
}
