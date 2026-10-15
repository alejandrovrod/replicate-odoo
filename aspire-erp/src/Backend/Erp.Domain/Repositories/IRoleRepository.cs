using Erp.Domain.Entities.Security;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for the Role &amp; Permission Manager
/// (.specify/modules/role-permission-manager/spec.md §3/§4). Tenant isolation is
/// AUTOMATIC through the global query filter (Constitution Article II.3): the
/// filtered reads never take a tenant predicate. The two <c>Unfiltered</c> reads exist
/// solely so handlers can distinguish "does not exist" (404) from "belongs to another
/// tenant" (403 <c>CROSS_TENANT_ASSIGNMENT_DENIED</c>) - the
/// <c>UserRepository.GetUserByEmailAsync</c> precedent for crossing the filter.
/// </summary>
public interface IRoleRepository
{
    /// <summary>Every role of the current tenant with its permission matrix.</summary>
    Task<IReadOnlyList<Role>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>One role of the current tenant with its matrix, or null.</summary>
    Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>One role of the current tenant by exact name, or null (duplicate check).</summary>
    Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Roles by id IGNORING the tenant filter (caller must compare
    /// <c>TenantId</c> itself - fail closed with 403 on mismatch).
    /// </summary>
    Task<IReadOnlyList<Role>> GetRolesByIdsUnfilteredAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>User headcount per role of the current tenant (for <c>GetRolesQuery</c>).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetUserCountsByRoleAsync(CancellationToken cancellationToken = default);

    /// <summary>Every user of the current tenant with roles + role names (for assignment UI).</summary>
    Task<IReadOnlyList<User>> GetUsersWithRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One user IGNORING the tenant filter, with roles loaded (caller compares
    /// <c>TenantId</c> itself: missing → 404, mismatch → 403).
    /// </summary>
    Task<User?> GetUserWithRolesUnfilteredAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Ids of the current tenant's users holding the System Manager role (last-admin guard).</summary>
    Task<IReadOnlyList<Guid>> GetSystemManagerHolderUserIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists a new functional role (<c>IsSystemDefault = false</c>, no matrix rows yet).</summary>
    Task CreateRoleAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>Replaces the whole permission matrix of one role (delete + insert).</summary>
    Task UpdatePermissionsMatrixAsync(Guid roleId, IReadOnlyList<DocTypePermission> permissions, CancellationToken cancellationToken = default);

    /// <summary>Replaces the whole role set of one user (PUT semantics, idempotent).</summary>
    Task AssignUserRolesAsync(Guid userId, Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default);
}
