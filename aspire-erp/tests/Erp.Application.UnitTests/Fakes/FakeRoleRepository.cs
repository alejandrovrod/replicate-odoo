using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="IRoleRepository"/> for the role &amp; permission handler tests.</summary>
public sealed class FakeRoleRepository : IRoleRepository
{
    private readonly List<Role> _roles = new();
    private readonly List<DocTypePermission> _permissions = new();
    private readonly List<User> _users = new();
    private readonly List<UserRole> _userRoles = new();

    public void SeedRoles(params Role[] roles) => _roles.AddRange(roles);

    public void SeedPermissions(params DocTypePermission[] permissions) => _permissions.AddRange(permissions);

    public void SeedUsers(params User[] users) => _users.AddRange(users);

    public void SeedUserRoles(params UserRole[] userRoles) => _userRoles.AddRange(userRoles);

    public static Role NewRole(
        Guid? roleId = null,
        Guid? tenantId = null,
        string name = "Support L1",
        bool isSystemDefault = false,
        string description = "Test role") => new()
        {
            Id = roleId ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Name = name,
            Description = description,
            IsSystemDefault = isSystemDefault,
        };

    public static User NewUser(Guid? userId = null, Guid? tenantId = null, string email = "user@example.com") => new()
    {
        Id = userId ?? Guid.NewGuid(),
        TenantId = tenantId ?? Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Email = email,
        FullName = "Test User",
        PasswordHash = "v1.plain",
    };

    public Task<IReadOnlyList<Role>> GetRolesWithPermissionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in _roles)
        {
            role.Permissions = _permissions.Where(p => p.RoleId == role.Id).ToList();
        }

        return Task.FromResult<IReadOnlyList<Role>>(_roles.OrderBy(r => r.Name).ToList());
    }

    public Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = _roles.FirstOrDefault(r => r.Id == roleId);
        if (role is not null)
        {
            role.Permissions = _permissions.Where(p => p.RoleId == role.Id).ToList();
        }

        return Task.FromResult(role);
    }

    public Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var role = _roles.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        if (role is not null)
        {
            role.Permissions = _permissions.Where(p => p.RoleId == role.Id).ToList();
        }

        return Task.FromResult(role);
    }

    public Task<IReadOnlyList<Role>> GetRolesByIdsUnfilteredAsync(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var matched = _roles.Where(r => roleIds.Contains(r.Id)).ToList();
        foreach (var role in matched)
        {
            role.Permissions = _permissions.Where(p => p.RoleId == role.Id).ToList();
        }

        return Task.FromResult<IReadOnlyList<Role>>(matched);
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetUserCountsByRoleAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, int> counts = _userRoles
            .GroupBy(ur => ur.RoleId)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(counts);
    }

    public Task<IReadOnlyList<User>> GetUsersWithRolesAsync(CancellationToken cancellationToken = default)
    {
        Attach();
        return Task.FromResult<IReadOnlyList<User>>(_users.OrderBy(u => u.Email).ToList());
    }

    public Task<User?> GetUserWithRolesUnfilteredAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Attach();
        return Task.FromResult(_users.FirstOrDefault(u => u.Id == userId));
    }

    public Task<IReadOnlyList<Guid>> GetSystemManagerHolderUserIdsAsync(CancellationToken cancellationToken = default)
    {
        var systemManagerRoleIds = _roles
            .Where(r => r.IsSystemDefault && r.Name == RoleValidator.SystemManagerRoleName)
            .Select(r => r.Id)
            .ToHashSet();

        IReadOnlyList<Guid> holders = _userRoles
            .Where(ur => systemManagerRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToList();

        return Task.FromResult(holders);
    }

    public Task CreateRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _roles.Add(role);
        return Task.CompletedTask;
    }

    public Task UpdatePermissionsMatrixAsync(Guid roleId, IReadOnlyList<DocTypePermission> permissions, CancellationToken cancellationToken = default)
    {
        _permissions.RemoveAll(p => p.RoleId == roleId);
        _permissions.AddRange(permissions);
        return Task.CompletedTask;
    }

    public Task AssignUserRolesAsync(Guid userId, Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        _userRoles.RemoveAll(ur => ur.UserId == userId);
        foreach (var roleId in roleIds.Distinct())
        {
            _userRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                RoleId = roleId,
            });
        }

        Attach();
        return Task.CompletedTask;
    }

    private void Attach()
    {
        foreach (var userRole in _userRoles)
        {
            userRole.Role = _roles.FirstOrDefault(r => r.Id == userRole.RoleId)!;
            userRole.User = _users.FirstOrDefault(u => u.Id == userRole.UserId)!;
        }

        foreach (var user in _users)
        {
            user.UserRoles = _userRoles.Where(ur => ur.UserId == user.Id).ToList();
        }
    }
}
