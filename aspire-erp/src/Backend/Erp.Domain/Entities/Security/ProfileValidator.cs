using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities.Security;

/// <summary>
/// Pure C# validation for the User Profile &amp; Security aggregates (module 15-user-profile).
/// No EF Core, no NuGet packages - Constitution Article I.2 keeps Erp.Domain dependency-free.
/// (The spec's plan mentions FluentValidation, but Erp.Application/Erp.Domain take zero NuGet
/// dependencies by Constitution I.3 / decision C2, so every module validates with static
/// Domain guards like <c>AccountValidator</c> instead.)
/// </summary>
public static class ProfileValidator
{
    /// <summary>
    /// New-password policy (spec §3.1): required, minimum length, confirmation match, and a
    /// baseline complexity floor (upper + lower + digit) so <c>AUTH_PASSWORD_POLICY_VIOLATION</c>
    /// has a precise meaning beyond length.
    /// </summary>
    /// <exception cref="AuthValidationException">A policy rule was violated.</exception>
    public static void EnsureValidNewPassword(string? newPassword, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new AuthValidationException(
                AuthErrorCodes.PasswordPolicyViolation,
                "A new password is required.");
        }

        if (newPassword.Length < ProfileRules.MinPasswordLength)
        {
            throw new AuthValidationException(
                AuthErrorCodes.PasswordPolicyViolation,
                $"The new password must be at least {ProfileRules.MinPasswordLength} characters long.");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            throw new AuthValidationException(
                AuthErrorCodes.PasswordPolicyViolation,
                "The new password and its confirmation do not match.");
        }

        bool hasUpper = false, hasLower = false, hasDigit = false;
        foreach (var ch in newPassword)
        {
            if (char.IsUpper(ch)) hasUpper = true;
            else if (char.IsLower(ch)) hasLower = true;
            else if (char.IsDigit(ch)) hasDigit = true;
        }

        if (!hasUpper || !hasLower || !hasDigit)
        {
            throw new AuthValidationException(
                AuthErrorCodes.PasswordPolicyViolation,
                "The new password must contain an uppercase letter, a lowercase letter and a digit.");
        }
    }

    /// <summary>
    /// TOTP verification-code shape guard: exactly 6 digits. Anything else is
    /// <c>MFA_INVALID_CODE</c> without touching the authenticator.
    /// </summary>
    /// <exception cref="AuthValidationException">The code shape is invalid.</exception>
    public static void EnsureValidTotpCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
        {
            throw new AuthValidationException(
                AuthErrorCodes.MfaInvalidCode,
                "The verification code must be a 6-digit number.");
        }
    }
}
