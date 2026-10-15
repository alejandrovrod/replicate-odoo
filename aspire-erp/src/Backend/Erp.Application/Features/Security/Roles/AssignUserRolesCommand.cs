using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Roles;

/// <summary>
/// Replaces the whole role set of a user - PUT semantics, idempotent (SEC-G3).
/// The change applies at the affected user's next login (JWT <c>roles</c>/<c>perms</c>
/// claims are minted at login; there is no server-side revocation).
/// <list type="bullet">
/// <item>Unknown user → <c>404 USER_ROLE_NOT_FOUND</c>; unknown role → <c>404 ROLE_NOT_FOUND</c>.</item>
/// <item>User or role of another tenant → <c>403 CROSS_TENANT_ASSIGNMENT_DENIED</c>.</item>
/// <item>Removing System Manager from the tenant's last holder → <c>409 CANNOT_REMOVE_LAST_ADMIN</c>.</item>
/// </list>
/// </summary>
public sealed record AssignUserRolesCommand(Guid UserId, Guid TenantId, IReadOnlyList<Guid> RoleIds)
    : ICommand<Result<Unit>>;

public sealed class AssignUserRolesCommandHandler : ICommandHandler<AssignUserRolesCommand, Result<Unit>>
{
    private readonly IRoleRepository _roles;

    public AssignUserRolesCommandHandler(IRoleRepository roles)
    {
        _roles = roles;
    }

    public async Task<Result<Unit>> HandleAsync(AssignUserRolesCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _roles.GetUserWithRolesUnfilteredAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<Unit>.Failure(AuthErrorCodes.UserRoleNotFound, "User not found in this tenant.");
        }

        if (user.TenantId != command.TenantId)
        {
            return Result<Unit>.Failure(
                AuthErrorCodes.CrossTenantAssignmentDenied,
                "The user belongs to another tenant.");
        }

        var requested = (command.RoleIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var matched = await _roles.GetRolesByIdsUnfilteredAsync(requested, cancellationToken);

        var missing = requested.Except(matched.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
        {
            return Result<Unit>.Failure(AuthErrorCodes.RoleNotFound, "Role not found.");
        }

        if (matched.Any(r => r.TenantId != command.TenantId))
        {
            return Result<Unit>.Failure(
                AuthErrorCodes.CrossTenantAssignmentDenied,
                "One or more roles belong to another tenant.");
        }

        var currentSystemManagerRoleIds = user.UserRoles
            .Where(ur => ur.Role is not null && RoleValidator.IsSystemManager(ur.Role))
            .Select(ur => ur.RoleId)
            .ToHashSet();

        if (currentSystemManagerRoleIds.Count > 0
            && !requested.Any(id => currentSystemManagerRoleIds.Contains(id)))
        {
            var otherHolders = (await _roles.GetSystemManagerHolderUserIdsAsync(cancellationToken))
                .Where(id => id != user.Id)
                .ToList();

            if (otherHolders.Count == 0)
            {
                return Result<Unit>.Failure(
                    AuthErrorCodes.CannotRemoveLastAdmin,
                    "The System Manager role cannot be removed from the last administrator of the tenant.");
            }
        }

        await _roles.AssignUserRolesAsync(user.Id, command.TenantId, requested, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
