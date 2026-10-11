using Erp.Application.Common;
using Erp.Application.Services;
using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Executes <see cref="VerifyLoginMfaCommand"/> (spec 16 §3.2): authenticates the challenge
/// ticket, then accepts either the current TOTP code or an unused backup code (redeemed
/// single-use on success). Failures increment <c>AccessFailedCount</c> and lock at
/// <see cref="ProfileRules.MaxFailedAccessAttempts"/>; success resets the counter and issues
/// the definitive JWT.
/// </summary>
public sealed class VerifyLoginMfaCommandHandler
    : ICommandHandler<VerifyLoginMfaCommand, Result<LoginResponseDto>>
{
    private readonly IUserRepository _users;
    private readonly ITokenGenerator _tokens;
    private readonly ITotpService _totp;
    private readonly IRecoveryCodeGenerator _recoveryCodes;
    private readonly IMfaChallengeTokenService _tickets;

    public VerifyLoginMfaCommandHandler(
        IUserRepository users,
        ITokenGenerator tokens,
        ITotpService totp,
        IRecoveryCodeGenerator recoveryCodes,
        IMfaChallengeTokenService tickets)
    {
        _users = users;
        _tokens = tokens;
        _totp = totp;
        _recoveryCodes = recoveryCodes;
        _tickets = tickets;
    }

    public async Task<Result<LoginResponseDto>> HandleAsync(
        VerifyLoginMfaCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            VerifyLoginMfaValidator.EnsureValid(command);

            // The ticket is the ONLY identity source; it must additionally belong to the
            // calling tenant (header) so tickets cannot hop across tenants.
            if (!_tickets.TryValidateTicket(command.MfaTicket, out Guid userId, out Guid ticketTenantId)
                || ticketTenantId != command.TenantId)
            {
                return MfaRejected();
            }

            var user = await _users.GetUserByIdAsync(userId, cancellationToken);
            if (user is null || user.TenantId != command.TenantId)
            {
                // Unknown user or cross-tenant ticket: same code as a wrong code (no oracle).
                return MfaRejected();
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Result<LoginResponseDto>.Failure(
                    AuthErrorCodes.AccountLocked,
                    "The account is temporarily locked due to too many failed attempts.");
            }

            // MFA switched off after the ticket was minted: the password proof still stands,
            // so complete the login instead of stranding the user.
            if (!user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.AuthenticatorKey))
            {
                return await IssueJwtAsync(user, cancellationToken);
            }

            string normalized = VerifyLoginMfaValidator.NormalizeCode(command.MfaCode);
            Guid? redeemedCodeId = null;

            bool totpOk = normalized.Length == 6
                && normalized.All(char.IsDigit)
                && _totp.VerifyCode(user.AuthenticatorKey, normalized);

            if (!totpOk)
            {
                redeemedCodeId = await MatchBackupCodeAsync(user, normalized, cancellationToken);
            }

            if (!totpOk && redeemedCodeId is null)
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= ProfileRules.MaxFailedAccessAttempts)
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.Add(ProfileRules.LockoutDuration);
                    await _users.UpdateUserAsync(user, cancellationToken);

                    return Result<LoginResponseDto>.Failure(
                        AuthErrorCodes.AccountLocked,
                        "The account is temporarily locked due to too many failed attempts.");
                }

                await _users.UpdateUserAsync(user, cancellationToken);
                return MfaRejected();
            }

            if (redeemedCodeId.HasValue)
            {
                await _users.MarkRecoveryCodeUsedAsync(redeemedCodeId.Value, cancellationToken);
            }

            return await IssueJwtAsync(user, cancellationToken);
        }
        catch (AuthValidationException ex)
        {
            return Result<LoginResponseDto>.Failure(ex.Code, ex.Message);
        }
    }

    /// <summary>Finds the unused stored code matching the normalized candidate, if any.</summary>
    private async Task<Guid?> MatchBackupCodeAsync(
        User user, string normalized, CancellationToken cancellationToken)
    {
        // The candidate is compared by hash only; plaintext codes never rest server-side.
        string candidateHash = _recoveryCodes.Hash(normalized);

        var active = await _users.GetActiveRecoveryCodesAsync(user.Id, cancellationToken);
        foreach (var row in active)
        {
            if (CryptographicEquals(row.CodeHash, candidateHash))
            {
                return row.Id;
            }
        }

        return null;
    }

    private static bool CryptographicEquals(string a, string b)
    {
        byte[] left = System.Text.Encoding.UTF8.GetBytes(a);
        byte[] right = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }

    private async Task<Result<LoginResponseDto>> IssueJwtAsync(User user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _users.UpdateUserAsync(user, cancellationToken);

        // Same JWT composition as the password-only login (permissions + roles claims).
        var permissions = await _users.GetUserPermissionsAsync(user.Id, cancellationToken);
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string token = _tokens.GenerateToken(user, permissions, roles);

        return Result<LoginResponseDto>.Success(
            LoginResponseDto.Authenticated(token, user.FullName, user.Email));
    }

    private static Result<LoginResponseDto> MfaRejected() =>
        Result<LoginResponseDto>.Failure(
            AuthErrorCodes.MfaInvalidLoginCode,
            "The two-factor code is incorrect or has expired.");
}
