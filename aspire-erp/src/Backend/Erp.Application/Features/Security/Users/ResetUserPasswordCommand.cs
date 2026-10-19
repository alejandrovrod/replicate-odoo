using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Users;

/// <summary>
/// Administrative password reset (spec D2 USER-G5): assigns the supplied temporary password or
/// generates a cryptographically secure one, hashes it with PBKDF2, and clears the brute-force
/// lockout so the user can sign in immediately. The plaintext is returned exactly once.
/// </summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string? TemporaryPassword = null)
    : ICommand<Result<ResetUserPasswordResult>>;

public sealed class ResetUserPasswordCommandHandler
    : ICommandHandler<ResetUserPasswordCommand, Result<ResetUserPasswordResult>>
{
    private readonly IUserAdminRepository _users;
    private readonly IPasswordHasher _passwords;

    public ResetUserPasswordCommandHandler(IUserAdminRepository users, IPasswordHasher passwords)
    {
        _users = users;
        _passwords = passwords;
    }

    public async Task<Result<ResetUserPasswordResult>> HandleAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdWithRolesAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<ResetUserPasswordResult>.Failure(
                AuthErrorCodes.UserNotFound,
                $"User '{command.UserId}' was not found in this tenant.");
        }

        var temporaryPassword = string.IsNullOrWhiteSpace(command.TemporaryPassword)
            ? TemporaryPasswordGenerator.Generate()
            : command.TemporaryPassword;

        try
        {
            ProfileValidator.EnsureValidNewPassword(temporaryPassword, temporaryPassword);

            user.PasswordHash = _passwords.Hash(temporaryPassword);
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            await _users.SaveAsync(user, cancellationToken);

            return Result<ResetUserPasswordResult>.Success(
                new ResetUserPasswordResult(temporaryPassword));
        }
        catch (AuthValidationException ex)
        {
            return Result<ResetUserPasswordResult>.Failure(ex.Code, ex.Message);
        }
    }
}
