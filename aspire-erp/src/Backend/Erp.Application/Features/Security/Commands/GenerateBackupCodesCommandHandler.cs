using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="GenerateBackupCodesCommand"/>: overwrites the user's recovery codes
/// with 10 fresh ones (spec §3.2 - previously issued codes are invalidated by the replace).
/// Requires MFA to be enabled, otherwise <c>MFA_NOT_ENABLED</c>.
/// </summary>
public sealed class GenerateBackupCodesCommandHandler
    : ICommandHandler<GenerateBackupCodesCommand, Result<IReadOnlyList<string>>>
{
    private readonly Erp.Domain.Repositories.IUserRepository _users;
    private readonly IRecoveryCodeGenerator _recoveryCodes;

    public GenerateBackupCodesCommandHandler(
        Erp.Domain.Repositories.IUserRepository users,
        IRecoveryCodeGenerator recoveryCodes)
    {
        _users = users;
        _recoveryCodes = recoveryCodes;
    }

    public async Task<Result<IReadOnlyList<string>>> HandleAsync(
        GenerateBackupCodesCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.TenantId != command.TenantId)
        {
            return Result<IReadOnlyList<string>>.Failure(AuthErrorCodes.InvalidPassword, "The user was not found.");
        }

        if (!user.TwoFactorEnabled)
        {
            return Result<IReadOnlyList<string>>.Failure(
                AuthErrorCodes.MfaNotEnabled,
                "Multi-factor authentication is not enabled for this user.");
        }

        IReadOnlyList<string> plaintextCodes = _recoveryCodes.Generate(ProfileRules.RecoveryCodeCount);
        await _users.ReplaceRecoveryCodesAsync(
            user,
            plaintextCodes.Select(code => MfaHandlerSupport.ToRecoveryCode(user, _recoveryCodes.Hash(code))).ToList(),
            cancellationToken);

        return Result<IReadOnlyList<string>>.Success(plaintextCodes);
    }
}
