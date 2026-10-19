using Erp.Domain.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="IUserAdminRepository"/> for the User Management handler tests. The
/// constructor pins a tenant and every read filters on it, mirroring the global tenant query
/// filter: rows of another tenant are invisible, which is what makes the cross-tenant email
/// case meaningful.
/// </summary>
public sealed class FakeUserAdminRepository : IUserAdminRepository
{
    private readonly Guid _tenantId;
    private readonly List<User> _users = new();
    private readonly List<Role> _roles = new();

    public FakeUserAdminRepository(Guid? tenantId = null)
    {
        _tenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111");
    }

    public Guid TenantId => _tenantId;

    public IReadOnlyList<User> Users => _users;

    public void SeedRole(Role role) => _roles.Add(role);

    public void SeedUser(User user) => _users.Add(user);

    public static Role SystemManager(Guid? id = null, Guid? tenantId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Name = RoleValidator.SystemManagerRoleName,
        Description = "Full access",
        IsSystemDefault = true,
    };

    public static Role FunctionalRole(string name, Guid? id = null, Guid? tenantId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Name = name,
        Description = name,
        IsSystemDefault = false,
    };

    public static User NewUser(
        string email,
        Guid? id = null,
        Guid? tenantId = null,
        bool isActive = true,
        params Role[] roles)
    {
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Email = email,
            FullName = "Test User",
            PasswordHash = "v1.hash",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            RowVersion = new byte[] { 1 },
        };

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = role.Id,
                Role = role,
            });
        }

        return user;
    }

    public Task<PagedResult<User>> GetPagedAsync(
        PagedRequest paging,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<User> visible = _users.Where(u => u.TenantId == _tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            visible = visible.Where(u =>
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = visible.OrderBy(u => u.FullName).ThenBy(u => u.Id).ToList();
        var page = ordered
            .Skip(paging.Skip)
            .Take(paging.SafePageSize)
            .ToList();

        return Task.FromResult(new PagedResult<User>(
            page,
            ordered.Count,
            paging.SafePageNumber,
            paging.SafePageSize));
    }

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == userId && u.TenantId == _tenantId));

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_users.Any(u =>
            u.TenantId == _tenantId
            && u.Email == email
            && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

    public Task<IReadOnlyList<Role>> GetRolesByIdsAsync(
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Role>>(
            _roles.Where(r => r.TenantId == _tenantId && roleIds.Contains(r.Id)).ToList());

    public Task AddAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        // AppDbContext stamps TenantId for ITenantEntity; the fake mirrors that contract.
        if (user.TenantId == Guid.Empty)
        {
            user.TenantId = _tenantId;
        }

        user.RowVersion ??= new byte[] { 1 };
        _users.Add(user);

        foreach (var roleId in roleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = roleId,
                Role = _roles.First(r => r.Id == roleId),
            });
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        User user,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        var desired = roleIds.Distinct().ToHashSet();

        foreach (var assignment in user.UserRoles
                     .Where(ur => !desired.Contains(ur.RoleId))
                     .ToList())
        {
            user.UserRoles.Remove(assignment);
        }

        var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        foreach (var roleId in desired.Where(id => !current.Contains(id)))
        {
            user.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = roleId,
                Role = _roles.First(r => r.Id == roleId),
            });
        }

        return Task.CompletedTask;
    }

    public Task SaveAsync(User user, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> CountOtherActiveSystemManagersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_users.Count(u =>
            u.TenantId == _tenantId
            && u.Id != userId
            && u.IsActive
            && u.UserRoles.Any(ur => ur.Role is not null && RoleValidator.IsSystemManager(ur.Role))));
}
