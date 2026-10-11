namespace Erp.Domain.Entities.Security;

/// <summary>
/// Stable machine-readable failure codes for the User Profile &amp; Security modules
/// (.specify/modules/15-user-profile/spec.md §4 and .specify/modules/16-auth-login-mfa/spec.md
/// §4). They flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where the controllers
/// map them to RFC 7807 status codes (see each controller for the exact mapping).
/// </summary>
/// <remarks>
/// Naming intentionally keeps each spec's UPPER_SNAKE codes verbatim on the wire
/// (e.g. <c>AUTH_INVALID_PASSWORD</c>): clients branch on the exact strings from the specs.
/// Module 16 deliberately defines <c>AUTH_MFA_INVALID_CODE</c> alongside module 15's
/// <c>MFA_INVALID_CODE</c>: the former is the login-challenge failure, the latter the
/// authenticated-profile verification failure - different endpoints, different payloads.
/// Every constant must resolve in <c>ErrorMessages{,.es}.resx</c> (enforced by
/// <c>ErrorCatalogCoverageTests</c>) and is mirrored to the frontend <c>error.json</c>
/// catalogs via <c>scripts/i18n-sync.mjs</c>.
/// </remarks>
public static class AuthErrorCodes
{
    public const string InvalidPassword = "AUTH_INVALID_PASSWORD";
    public const string PasswordPolicyViolation = "AUTH_PASSWORD_POLICY_VIOLATION";
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string MfaInvalidCode = "MFA_INVALID_CODE";
    public const string MfaNotEnabled = "MFA_NOT_ENABLED";

    /// <summary>
    /// Informational payload marker (spec 16 §4): HTTP 200 login responses carry
    /// <c>IsMfaRequired = true</c> when the password verified but the second factor is pending.
    /// </summary>
    public const string MfaRequired = "AUTH_MFA_REQUIRED";

    /// <summary>Login-challenge failure: wrong/expired TOTP or backup code, or unusable challenge ticket.</summary>
    public const string MfaInvalidLoginCode = "AUTH_MFA_INVALID_CODE";
}
