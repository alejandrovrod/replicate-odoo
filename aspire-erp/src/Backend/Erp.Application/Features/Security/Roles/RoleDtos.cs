using Erp.Domain.Entities.Security;

namespace Erp.Application.Features.Security.Roles;

/// <summary>One matrix cell-row (DocType × six actions), mirroring <see cref="DocTypePermission"/>.</summary>
public sealed record DocTypePermissionDto(
    string DocType,
    bool CanRead,
    bool CanWrite,
    bool CanCreate,
    bool CanDelete,
    bool CanSubmit,
    bool CanCancel)
{
    public static DocTypePermissionDto From(DocTypePermission p) =>
        new(p.DocType, p.CanRead, p.CanWrite, p.CanCreate, p.CanDelete, p.CanSubmit, p.CanCancel);

    public DocTypePermission ToEntity(Guid tenantId, Guid roleId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        RoleId = roleId,
        DocType = DocType,
        CanRead = CanRead,
        CanWrite = CanWrite,
        CanCreate = CanCreate,
        CanDelete = CanDelete,
        CanSubmit = CanSubmit,
        CanCancel = CanCancel,
    };
}

/// <summary>Role list row (SEC-G2): system flag exposed, headcount included.</summary>
public sealed record RoleDto(Guid Id, string Name, string Description, bool IsSystemDefault, int UserCount)
{
    public static RoleDto From(Role role, int userCount) =>
        new(role.Id, role.Name, role.Description, role.IsSystemDefault, userCount);
}

/// <summary>Role detail with its full permission matrix (SEC-G1).</summary>
public sealed record RolePermissionsDto(
    Guid RoleId,
    string RoleName,
    bool IsSystemDefault,
    IReadOnlyList<DocTypePermissionDto> Permissions)
{
    public static RolePermissionsDto From(Role role) =>
        new(role.Id, role.Name, role.IsSystemDefault,
            role.Permissions.Select(DocTypePermissionDto.From).OrderBy(p => p.DocType).ToList());
}

/// <summary>One user with its assigned role names (assignment dialog read).</summary>
public sealed record UserWithRolesDto(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles)
{
    public static UserWithRolesDto From(User user) =>
        new(user.Id, user.Email, user.FullName,
            user.UserRoles
                .Where(ur => ur.Role is not null)
                .Select(ur => ur.Role!.Name)
                .OrderBy(n => n)
                .ToList());
}
