using Erp.Domain.Entities.Security;
using Erp.Domain.Exceptions;

namespace Erp.Application.Features.Security.Commands;

/// <summary>
/// Application-side guard for <see cref="VerifyLoginMfaCommand"/> (tasks.md Backend item).
/// Shape rule mirrors the spec (§3.2: <c>MfaCode</c> length 6..8): after normalization
/// (separators stripped, uppercased) the code must be 6-8 alphanumeric chars - a 6-digit
/// TOTP or an 8-char backup code. Every violation reports <c>AUTH_MFA_INVALID_CODE</c> so
/// malformed and wrong codes are indistinguishable on the wire.
/// </summary>
public static class VerifyLoginMfaValidator
{
    /// <summary>Strips separators (<c>-</c>, spaces) and uppercases for comparison.</summary>
    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(code.Length);
        foreach (char c in code)
        {
            if (c is '-' or ' ')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>Validates the command shape.</summary>
    /// <exception cref="AuthValidationException">A rule was violated.</exception>
    public static void EnsureValid(VerifyLoginMfaCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.MfaTicket))
        {
            throw new AuthValidationException(
                AuthErrorCodes.MfaInvalidLoginCode,
                "The authentication challenge is missing or has expired.");
        }

        string normalized = NormalizeCode(command.MfaCode);
        if (normalized.Length < 6
            || normalized.Length > 8
            || !normalized.All(c => char.IsLetterOrDigit(c)))
        {
            throw new AuthValidationException(
                AuthErrorCodes.MfaInvalidLoginCode,
                "The two-factor code is incorrect or has expired.");
        }
    }
}
