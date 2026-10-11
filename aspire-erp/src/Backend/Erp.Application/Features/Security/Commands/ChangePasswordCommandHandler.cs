using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="ChangePasswordCommand"/> (spec §3.1): rejects locked accounts with
/// <c>AUTH_ACCOUNT_LOCKED</c>, verifies the current password (mismatch increments
/// <c>AccessFailedCount</c> and locks at <see cref="ProfileRules.MaxFailedAccessAttempts"/>),
/// then validates the policy, re-hashes and resets the counter. Returns
/// <c>Result&lt;Unit&gt;.Success()</c> on success.
/// </summary>
public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, Result<Unit>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwords;

    public ChangePasswordCommandHandler(IUserRepository users, IPasswordHasher passwords)
    {
        _users = users;
        _passwords = passwords;
    }

    public async Task<Result<Unit>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            ChangePasswordValidator.EnsureValid(command);

            var user = await _users.GetUserByIdAsync(command.UserId, cancellationToken);
            if (user is null || user.TenantId != command.TenantId)
            {
                // Unknown user or cross-tenant access: indistinguishable from a bad password
                // on the wire (no user enumeration).
                return Result<Unit>.Failure(AuthErrorCodes.InvalidPassword, "The current password is incorrect.");
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Result<Unit>.Failure(
                    AuthErrorCodes.AccountLocked,
                    "The account is temporarily locked due to too many failed attempts.");
            }

            if (!_passwords.Verify(command.CurrentPassword, user.PasswordHash))
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= ProfileRules.MaxFailedAccessAttempts)
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.Add(ProfileRules.LockoutDuration);
                    await _users.UpdateUserAsync(user, cancellationToken);

                    return Result<Unit>.Failure(
                        AuthErrorCodes.AccountLocked,
                        "The account is temporarily locked due to too many failed attempts.");
                }

                await _users.UpdateUserAsync(user, cancellationToken);

                return Result<Unit>.Failure(
                    AuthErrorCodes.InvalidPassword,
                    "The current password is incorrect.");
            }

            // Policy was pre-checked by the validator, but the hasher upgrade path (legacy
            // plaintext verifier) must not skip it: re-assert before persisting.
            ProfileValidator.EnsureValidNewPassword(command.NewPassword, command.ConfirmPassword);

            user.PasswordHash = _passwords.Hash(command.NewPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await _users.UpdateUserAsync(user, cancellationToken);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (AuthValidationException ex)
        {
            return Result<Unit>.Failure(ex.Code, ex.Message);
        }
    }
}
