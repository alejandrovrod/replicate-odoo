using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="EnableMfaCommand"/>: stages a fresh TOTP secret on the user (leaving
/// <c>TwoFactorEnabled</c> false until verification) and issues the initial recovery-code set,
/// invalidating any previously issued codes. Plaintext codes are returned exactly once - only
/// SHA-256 hashes are persisted (spec §2).
/// </summary>
public sealed class EnableMfaCommandHandler : ICommandHandler<EnableMfaCommand, Result<EnableMfaResult>>
{
    private const string Issuer = "AspireERP";

    private readonly IUserRepository _users;
    private readonly ITotpService _totp;
    private readonly IRecoveryCodeGenerator _recoveryCodes;

    public EnableMfaCommandHandler(
        IUserRepository users,
        ITotpService totp,
        IRecoveryCodeGenerator recoveryCodes)
    {
        _users = users;
        _totp = totp;
        _recoveryCodes = recoveryCodes;
    }

    public async Task<Result<EnableMfaResult>> HandleAsync(EnableMfaCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.TenantId != command.TenantId)
        {
            return Result<EnableMfaResult>.Failure(AuthErrorCodes.InvalidPassword, "The user was not found.");
        }

        string secret = _totp.GenerateSecret();
        IReadOnlyList<string> plaintextCodes = _recoveryCodes.Generate(ProfileRules.RecoveryCodeCount);

        user.AuthenticatorKey = secret;
        user.TwoFactorEnabled = false;
        await _users.UpdateUserAsync(user, cancellationToken);
        await _users.ReplaceRecoveryCodesAsync(
            user,
            plaintextCodes.Select(code => MfaHandlerSupport.ToRecoveryCode(user, _recoveryCodes.Hash(code))).ToList(),
            cancellationToken);

        string uri = _totp.BuildAuthenticatorUri(secret, user.Email, Issuer);
        return Result<EnableMfaResult>.Success(new EnableMfaResult(secret, uri, plaintextCodes));
    }
}
