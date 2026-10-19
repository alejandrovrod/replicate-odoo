using Erp.Domain.Entities.Security;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// User Management read model (user-management spec D2/D4). Exposes both role ids (checkbox
/// state of the edit dialog) and role names (grid badges), plus the lifecycle and concurrency
/// fields the client round-trips on PUT.
/// </summary>
public sealed record UserAdminDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAt,
    byte[]? RowVersion,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleNames)
{
    public static UserAdminDto From(User user)
    {
        var assignments = user.UserRoles
            .Where(ur => ur.Role is not null)
            .OrderBy(ur => ur.Role!.Name)
            .ToList();

        return new UserAdminDto(
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            user.CreatedAt,
            user.RowVersion,
            assignments.Select(ur => ur.RoleId).ToList(),
            assignments.Select(ur => ur.Role!.Name).ToList());
    }
}

/// <summary>Result of an administrative password reset (plaintext returned exactly once).</summary>
public sealed record ResetUserPasswordResult(string TemporaryPassword);

/// <summary>
/// Result of a user creation: the persisted read model plus the initial password (generated or
/// supplied). The plaintext is returned exactly once so the administrator can hand it over;
/// it is never persisted in clear text.
/// </summary>
public sealed record CreateUserResult(UserAdminDto User, string TemporaryPassword);
