using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRoleRepository"/> (spec §3/§4). Reads rely on the
/// global tenant query filter (Constitution Article II.3) - no manual
/// <c>.Where(TenantId)</c> anywhere below, except the two explicitly <c>Unfiltered</c>
/// reads (the <c>UserRepository.GetUserByEmailAsync</c> precedent) whose callers compare
/// <c>TenantId</c> themselves to tell 404 apart from 403.
/// </summary>
public sealed class RolesRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RolesRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Role>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken = default) =>
        await _context.Roles
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public async Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesByIdsUnfilteredAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Role>();
        }

        return await _context.Roles
            .IgnoreQueryFilters()
            .Include(r => r.Permissions)
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetUserCountsByRoleAsync(CancellationToken cancellationToken = default) =>
        await _context.UserRoles
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<User>> GetUsersWithRolesAsync(CancellationToken cancellationToken = default) =>
        await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

    public async Task<User?> GetUserWithRolesUnfilteredAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetSystemManagerHolderUserIdsAsync(CancellationToken cancellationToken = default)
    {
        var systemManagerRoleIds = await _context.Roles
            .Where(r => r.IsSystemDefault && r.Name == RoleValidator.SystemManagerRoleName)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (systemManagerRoleIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return await _context.UserRoles
            .Where(ur => systemManagerRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task CreateRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        await _context.Roles.AddAsync(role, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePermissionsMatrixAsync(Guid roleId, IReadOnlyList<DocTypePermission> permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var existing = await _context.DocTypePermissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.DocTypePermissions.RemoveRange(existing);
        }

        foreach (var row in permissions)
        {
            await _context.DocTypePermissions.AddAsync(row, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignUserRolesAsync(Guid userId, Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.UserRoles.RemoveRange(existing);
        }

        foreach (var roleId in roleIds.Distinct())
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                RoleId = roleId,
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
