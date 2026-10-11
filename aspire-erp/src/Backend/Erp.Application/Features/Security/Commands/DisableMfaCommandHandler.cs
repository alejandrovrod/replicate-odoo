using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="DisableMfaCommand"/> (plan Phase 1.2): proves possession with a current
/// TOTP code, then clears the secret, the flag and every issued backup code. Disabling an
/// already-disabled authenticator reports <c>MFA_NOT_ENABLED</c>.
/// </summary>
public sealed class DisableMfaCommandHandler : ICommandHandler<DisableMfaCommand, Result<Unit>>
{
    private readonly Erp.Domain.Repositories.IUserRepository _users;
    private readonly ITotpService _totp;

    public DisableMfaCommandHandler(Erp.Domain.Repositories.IUserRepository users, ITotpService totp)
    {
        _users = users;
        _totp = totp;
    }

    public async Task<Result<Unit>> HandleAsync(DisableMfaCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            ProfileValidator.EnsureValidTotpCode(command.Code);

            var user = await _users.GetUserByIdAsync(command.UserId, cancellationToken);
            if (user is null || user.TenantId != command.TenantId)
            {
                return Result<Unit>.Failure(AuthErrorCodes.InvalidPassword, "The user was not found.");
            }

            if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.AuthenticatorKey))
            {
                return Result<Unit>.Failure(
                    AuthErrorCodes.MfaNotEnabled,
                    "Multi-factor authentication is not enabled for this user.");
            }

            if (!_totp.VerifyCode(user.AuthenticatorKey, command.Code.Trim()))
            {
                return Result<Unit>.Failure(
                    AuthErrorCodes.MfaInvalidCode,
                    "The verification code is incorrect or has expired.");
            }

            user.TwoFactorEnabled = false;
            user.AuthenticatorKey = null;
            await _users.UpdateUserAsync(user, cancellationToken);
            await _users.ReplaceRecoveryCodesAsync(user, new List<UserRecoveryCode>(), cancellationToken);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (AuthValidationException ex)
        {
            return Result<Unit>.Failure(ex.Code, ex.Message);
        }
    }
}
