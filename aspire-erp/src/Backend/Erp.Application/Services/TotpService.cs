using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Erp.Application.Services;

/// <summary>
/// RFC 6238 TOTP (SHA-1, 30-second step, 6 digits) over a Base32 shared secret.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int SecretSizeBytes = 20;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(SecretSizeBytes);
        return Base32Encode(bytes);
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
            key = Base32Decode(secret.Trim().ToUpperInvariant());
        }
        catch (FormatException)
        {
            return false;
        }

        long currentCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (long counter = currentCounter - allowedStepDrift; counter <= currentCounter + allowedStepDrift; counter++)
        {
            if (counter < 0)
            {
                continue;
            }

            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(ComputeCode(key, counter)),
                Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    public string ComputeCode(string secret, long counter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return ComputeCode(Base32Decode(secret.Trim().ToUpperInvariant()), counter);
    }

    private static string ComputeCode(byte[] key, long counter)
    {
        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        byte[] hash = HMACSHA1.HashData(key, counterBytes);
        int offset = hash[^1] & 0x0F;
        int binaryCode =
            ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        int otp = binaryCode % (int)Math.Pow(10, Digits);
        return otp.ToString("D" + Digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    internal static string Base32Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var result = new StringBuilder();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Base32Alphabet[(buffer >> bitsLeft) & 31]);
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return result.ToString();
    }

    internal static byte[] Base32Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        // Padding is never emitted by GenerateSecret but is tolerated on input.
        string input = encoded.TrimEnd('=');
        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in input)
        {
            int value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"Invalid Base32 character '{c}'.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return output.ToArray();
    }
}
