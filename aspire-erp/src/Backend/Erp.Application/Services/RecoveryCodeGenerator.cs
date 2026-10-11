using System.Security.Cryptography;
using System.Text;

namespace Erp.Application.Services;

/// <summary>
/// Default recovery-code generator: codes shaped <c>XXXX-XXXX</c> from an unambiguous
/// alphabet (no 0/O, 1/I/L), hashed with SHA-256 for storage.
/// </summary>
public sealed class RecoveryCodeGenerator : IRecoveryCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int GroupSize = 4;
    private const int GroupCount = 2;

    public IReadOnlyList<string> Generate(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");
        }

        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            var groups = new string[GroupCount];
            for (int g = 0; g < GroupCount; g++)
            {
                var chars = new char[GroupSize];
                for (int c = 0; c < GroupSize; c++)
                {
                    chars[c] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
                }

                groups[g] = new string(chars);
            }

            codes.Add(string.Join("-", groups));
        }

        return codes;
    }

    public string Hash(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)));
        return Convert.ToHexString(bytes);
    }

    public string Normalize(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var builder = new StringBuilder(code.Length);
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
}
