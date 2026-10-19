using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Executes <see cref="UpdateUserCommand"/>: tenant lookup, fail-fast RowVersion
/// compare-and-swap, field/role validation, last-active-administrator guard when the edit
/// removes the System Manager role or deactivates the account, and persistence.
/// </summary>
public sealed class UpdateUserCommandHandler
    : ICommandHandler<UpdateUserCommand, Result<UserAdminDto>>
{
    private readonly IUserAdminRepository _users;

    public UpdateUserCommandHandler(IUserAdminRepository users)
    {
        _users = users;
    }

    public async Task<Result<UserAdminDto>> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var violations = UpdateUserCommandValidator.Validate(command);
        if (violations.Count > 0)
        {
            return Result<UserAdminDto>.Failure(
                AuthErrorCodes.InvalidUserData,
                string.Join(" ", violations));
        }

        var user = await _users.GetByIdWithRolesAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure(
                AuthErrorCodes.UserNotFound,
                $"User '{command.UserId}' was not found in this tenant.");
        }

        try
        {
            UserValidator.EnsureRowVersion(user.RowVersion, command.RowVersion);

            var roleIds = command.RoleIds.Distinct().ToList();
            var roles = await _users.GetRolesByIdsAsync(roleIds, cancellationToken);
            if (roles.Count != roleIds.Count)
            {
                return Result<UserAdminDto>.Failure(
                    AuthErrorCodes.InvalidUserData,
                    "One or more roles do not belong to this tenant.");
            }

            var currentlySystemManager = user.UserRoles
                .Any(ur => ur.Role is not null && RoleValidator.IsSystemManager(ur.Role));
            var remainsSystemManager = roles.Any(RoleValidator.IsSystemManager);
            var targetIsActive = command.IsActive ?? user.IsActive;

            if (currentlySystemManager && user.IsActive)
            {
                var losesAdminPower = !remainsSystemManager || !targetIsActive;
                if (losesAdminPower)
                {
                    var otherAdmins = await _users.CountOtherActiveSystemManagersAsync(
                        user.Id,
                        cancellationToken);
                    UserValidator.EnsureAnotherActiveAdminRemains(
                        isActiveSystemManager: true,
                        hasAnotherActiveSystemManager: otherAdmins > 0);
                }
            }

            user.FullName = command.FullName.Trim();
            user.IsActive = targetIsActive;

            await _users.UpdateAsync(user, roleIds, cancellationToken);

            var updated = await _users.GetByIdWithRolesAsync(user.Id, cancellationToken);
            return Result<UserAdminDto>.Success(UserAdminDto.From(updated ?? user));
        }
        catch (AuthValidationException ex)
        {
            return Result<UserAdminDto>.Failure(ex.Code, ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // A token that changed between the read and the save is the same client-facing
            // failure as a stale token: 409 STALE_ROW_VERSION.
            return Result<UserAdminDto>.Failure(AuthErrorCodes.StaleRowVersion, ex.Message);
        }
    }
}
