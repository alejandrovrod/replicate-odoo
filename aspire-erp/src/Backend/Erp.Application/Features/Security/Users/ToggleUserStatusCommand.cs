using Erp.Application.Common;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Activates or deactivates a user (spec D2 USER-G4). Deactivating the tenant's last active
/// System Manager is refused with <c>CANNOT_REMOVE_LAST_ADMIN</c> (409); the change is
/// idempotent when the account is already in the requested state.
/// </summary>
public sealed record ToggleUserStatusCommand(Guid UserId, bool IsActive)
    : ICommand<Result<UserAdminDto>>;

public sealed class ToggleUserStatusCommandHandler
    : ICommandHandler<ToggleUserStatusCommand, Result<UserAdminDto>>
{
    private readonly IUserAdminRepository _users;

    public ToggleUserStatusCommandHandler(IUserAdminRepository users)
    {
        _users = users;
    }

    public async Task<Result<UserAdminDto>> HandleAsync(
        ToggleUserStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRolesAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserAdminDto>.Failure(
                AuthErrorCodes.UserNotFound,
                $"User '{command.UserId}' was not found in this tenant.");
        }

        if (user.IsActive == command.IsActive)
        {
            return Result<UserAdminDto>.Success(UserAdminDto.From(user));
        }

        try
        {
            var isActiveSystemManager = user.UserRoles
                .Any(ur => ur.Role is not null && RoleValidator.IsSystemManager(ur.Role));

            if (!command.IsActive && isActiveSystemManager)
            {
                var otherAdmins = await _users.CountOtherActiveSystemManagersAsync(
                    user.Id,
                    cancellationToken);
                UserValidator.EnsureAnotherActiveAdminRemains(
                    isActiveSystemManager: true,
                    hasAnotherActiveSystemManager: otherAdmins > 0);
            }

            user.IsActive = command.IsActive;
            await _users.SaveAsync(user, cancellationToken);

            return Result<UserAdminDto>.Success(UserAdminDto.From(user));
        }
        catch (AuthValidationException ex)
        {
            return Result<UserAdminDto>.Failure(ex.Code, ex.Message);
        }
    }
}
