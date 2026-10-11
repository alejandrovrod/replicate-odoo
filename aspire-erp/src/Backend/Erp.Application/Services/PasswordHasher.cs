using System.Security.Cryptography;
using System.Text;

namespace Erp.Application.Services;

/// <summary>
/// PBKDF2-SHA256 password hasher (210.000 iterations, 128-bit salt, 256-bit subkey).
/// Wire format is <c>v1.{iterations}.{saltB64}.{subkeyB64}</c> so parameters can evolve
/// without breaking stored verifiers. A stored value WITHOUT the <c>v1.</c> prefix is treated
/// as a legacy plaintext import (see <c>LoginCommandHandler</c>'s plain-text era and the
/// <c>demo/demo</c> development seed): it verifies by ordinal comparison so existing rows keep
/// working, and callers re-hash on success.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const string Prefix = "v1";
    private const int DefaultIterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int SubkeySizeBytes = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, DefaultIterations, HashAlgorithmName.SHA256, SubkeySizeBytes);

        return string.Join('.',
            Prefix,
            DefaultIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }

    public bool Verify(string password, string storedVerifier)
    {
        if (password is null || storedVerifier is null)
        {
            return false;
        }

        if (!storedVerifier.StartsWith(Prefix + ".", StringComparison.Ordinal))
        {
            // Legacy plaintext import: ordinal compare, no timing hardening claimed. Callers
            // upgrade the stored value to <c>Hash(...)</c> after a successful verification.
            return string.Equals(password, storedVerifier, StringComparison.Ordinal);
        }

        var parts = storedVerifier.Split('.');
        if (parts.Length != 4
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt, expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
