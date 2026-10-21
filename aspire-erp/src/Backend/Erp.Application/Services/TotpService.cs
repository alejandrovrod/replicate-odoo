using System.Globalization;
using OtpNet;

namespace Erp.Application.Services;

/// <summary>
/// RFC 6238 TOTP powered by industry-standard Otp.NET (as in AssetHub).
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int SecretSizeBytes = 20;

    public string GenerateSecret()
    {
        byte[] bytes = KeyGeneration.GenerateRandomKey(SecretSizeBytes);
        return Base32Encoding.ToString(bytes);
    }

    public string BuildAuthenticatorUri(string secret, string accountName, string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        string label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}";
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";
    }

    public bool VerifyCode(string secret, string code, int allowedStepDrift = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string normalized = code.Trim();
        if (normalized.Length != Digits || !normalized.All(char.IsDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32Encoding.ToBytes(secret.Trim().ToUpperInvariant());
        }
        catch (Exception)
        {
            return false;
        }

        var totp = new Totp(key, step: StepSeconds, totpSize: Digits);
        // Tolerate clock skew between phone and server (up to 3 steps = 90 seconds drift)
        return totp.VerifyTotp(normalized, out _, new VerificationWindow(previous: 3, future: 3));
    }

    public string ComputeCode(string secret, long counter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        byte[] key = Base32Encoding.ToBytes(secret.Trim().ToUpperInvariant());
        var totp = new Totp(key, step: StepSeconds, totpSize: Digits);
        // OtpNet computes code based on step timestamp
        var dt = DateTimeOffset.FromUnixTimeSeconds(counter * StepSeconds).UtcDateTime;
        return totp.ComputeTotp(dt);
    }
}
