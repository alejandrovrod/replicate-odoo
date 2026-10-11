namespace Erp.Application.Services;

/// <summary>
/// RFC 6238 TOTP for MFA (spec 15-user-profile §3.2). BCL-only (HMAC-SHA1 + Base32) so
/// Erp.Application keeps zero NuGet dependencies (Constitution I.3) - no OtpNet package.
/// The spec's replay protection ("Identity tracking the last used timestamp/code") is honored
/// by staging the secret on <c>EnableMfaCommand</c> and only flipping
/// <c>User.TwoFactorEnabled</c> on a successful <c>VerifyMfaCommand</c>: a code is valid for a
/// single 30-second step and the enable handshake cannot be replayed once completed.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a fresh 160-bit shared secret, Base32 (no padding).</summary>
    string GenerateSecret();

    /// <summary>Builds the <c>otpauth://totp/...</c> URI rendered as a QR code by the SPA.</summary>
    string BuildAuthenticatorUri(string secret, string accountName, string issuer);

    /// <summary>Verifies a 6-digit code against the secret within the given clock-skew window.</summary>
    /// <param name="allowedStepDrift">Steps before/after the current one accepted (±1 default).</param>
    bool VerifyCode(string secret, string code, int allowedStepDrift = 1);

    /// <summary>Computes the expected code for an explicit counter (test seam).</summary>
    string ComputeCode(string secret, long counter);
}
