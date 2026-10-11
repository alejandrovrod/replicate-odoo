using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Application-side guard for <see cref="ChangePasswordCommand"/> (tasks.md Backend item).
/// Required-field checks live here at the slice boundary; the password policy itself is a
/// Domain invariant owned by <see cref="ProfileValidator"/> (Constitution: domain rules stay
/// in Erp.Domain, BCL-only, unit-tested without a database).
/// </summary>
public static class ChangePasswordValidator
{
    /// <summary>Validates the command shape and the new-password policy.</summary>
    /// <exception cref="AuthValidationException">A rule was violated.</exception>
    public static void EnsureValid(ChangePasswordCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.UserId == Guid.Empty)
        {
            throw new AuthValidationException(
                AuthErrorCodes.PasswordPolicyViolation,
                "The user identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(command.CurrentPassword))
        {
            throw new AuthValidationException(
                AuthErrorCodes.InvalidPassword,
                "The current password is required.");
        }

        ProfileValidator.EnsureValidNewPassword(command.NewPassword, command.ConfirmPassword);
    }
}
