using Erp.Domain.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserAdminRepository"/> (user-management spec D2/D3).
/// Every read relies on the global tenant query filter (Constitution Article II.3): no method
/// below hand-writes a <c>TenantId</c> predicate, so cross-tenant rows are invisible by
/// construction. Mutations still stamp <c>TenantId</c> explicitly on join rows.
/// </summary>
public sealed class UserAdminRepository : IUserAdminRepository
{
    private readonly AppDbContext _context;

    public UserAdminRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<User>> GetPagedAsync(
        PagedRequest paging,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
        }

        // Deterministic pages require ORDER BY; FullName then Id is stable and index-friendly.
        query = query.OrderBy(u => u.FullName).ThenBy(u => u.Id);

        return await query.ToPagedResultAsync(paging, cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default) =>
        await _context.Users
            .AnyAsync(
                u => u.Email == email
                    && (!excludeUserId.HasValue || u.Id != excludeUserId.Value),
                cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesByIdsAsync(
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Role>();
        }

        return await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleIds);

        await _context.Users.AddAsync(user, cancellationToken);

        foreach (var roleId in roleIds.Distinct())
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = roleId,
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleIds);

        var desired = roleIds.Distinct().ToList();
        var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        foreach (var assignment in user.UserRoles
                     .Where(ur => !desired.Contains(ur.RoleId))
                     .ToList())
        {
            _context.UserRoles.Remove(assignment);
        }

        foreach (var roleId in desired.Where(id => !current.Contains(id)))
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = roleId,
            }, cancellationToken);
        }

        await SaveWithConcurrencyTranslationAsync(user, cancellationToken);
    }

    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await SaveWithConcurrencyTranslationAsync(user, cancellationToken);
    }

    public async Task<int> CountOtherActiveSystemManagersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var systemManagerRoleIds = await _context.Roles
            .Where(r => r.IsSystemDefault && r.Name == RoleValidator.SystemManagerRoleName)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (systemManagerRoleIds.Count == 0)
        {
            return 0;
        }

        return await _context.Users
            .CountAsync(
                u => u.Id != userId
                    && u.IsActive
                    && u.UserRoles.Any(ur => systemManagerRoleIds.Contains(ur.RoleId)),
                cancellationToken);
    }

    private async Task SaveWithConcurrencyTranslationAsync(
        User user,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(User), user.Id, ex);
        }
    }
}
