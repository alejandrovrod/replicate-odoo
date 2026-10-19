using Erp.Domain.Common;
using Erp.Domain.Entities.Security;

namespace Erp.Domain.Repositories;

/// <summary>
/// Data-access contract for User Management (user-management spec D2/D4). Reads rely on the
/// global tenant query filter (Constitution Article II.3), so no method takes a tenant
/// predicate: a user or role of another tenant is simply not visible here.
/// </summary>
public interface IUserAdminRepository
{
    /// <summary>One page of the current tenant's users, roles loaded, optionally text-filtered.</summary>
    Task<PagedResult<User>> GetPagedAsync(
        PagedRequest paging,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>One user of the current tenant with role assignments, or null.</summary>
    Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-scoped email uniqueness probe. The tenant filter already scopes the query, so the
    /// same address in another tenant is invisible (and therefore legal - USER-G2).
    /// </summary>
    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Roles of the current tenant matching the ids (missing ids are absent).</summary>
    Task<IReadOnlyList<Role>> GetRolesByIdsAsync(
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default);

    /// <summary>Persists a new user and its role assignments in one transaction.</summary>
    Task AddAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default);

    /// <summary>Persists user fields and atomically replaces its whole role set (PUT semantics).</summary>
    Task UpdateAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default);

    /// <summary>Persists field changes without touching role assignments (status, password).</summary>
    Task SaveAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of ACTIVE System Manager holders of the current tenant excluding
    /// <paramref name="userId"/> (last-active-administrator guard).
    /// </summary>
    Task<int> CountOtherActiveSystemManagersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
