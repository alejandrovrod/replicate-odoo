using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="VerifyMfaCommand"/>: checks the 6-digit TOTP code against the staged
/// secret and flips <c>TwoFactorEnabled</c> on success. A wrong/expired code (or a missing
/// staged secret) reports <c>MFA_INVALID_CODE</c> - never whether a secret was staged.
/// </summary>
public sealed class VerifyMfaCommandHandler : ICommandHandler<VerifyMfaCommand, Result<Unit>>
{
    private readonly Erp.Domain.Repositories.IUserRepository _users;
    private readonly ITotpService _totp;

    public VerifyMfaCommandHandler(Erp.Domain.Repositories.IUserRepository users, ITotpService totp)
    {
        _users = users;
        _totp = totp;
    }

    public async Task<Result<Unit>> HandleAsync(VerifyMfaCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            ProfileValidator.EnsureValidTotpCode(command.Code);

            var user = await _users.GetUserByIdAsync(command.UserId, cancellationToken);
            if (user is null
                || user.TenantId != command.TenantId
                || string.IsNullOrWhiteSpace(user.AuthenticatorKey)
                || !_totp.VerifyCode(user.AuthenticatorKey, command.Code.Trim()))
            {
                return Result<Unit>.Failure(
                    AuthErrorCodes.MfaInvalidCode,
                    "The verification code is incorrect or has expired.");
            }

            user.TwoFactorEnabled = true;
            await _users.UpdateUserAsync(user, cancellationToken);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (AuthValidationException ex)
        {
            return Result<Unit>.Failure(ex.Code, ex.Message);
        }
    }
}
