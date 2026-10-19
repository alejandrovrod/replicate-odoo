using System.Security.Cryptography;

namespace Erp.Application.Services;

/// <summary>
/// Generates cryptographically secure temporary passwords for administrative resets
/// (user-management spec D2 USER-G5). The output always satisfies
/// <see cref="Erp.Domain.Entities.Security.ProfileValidator"/> (upper + lower + digit) and
/// avoids look-alike characters (O/0, I/l) that get mistyped when read aloud.
/// </summary>
public static class TemporaryPasswordGenerator
{
    public const int DefaultLength = 16;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*";
    private const string All = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = DefaultLength)
    {
        if (length < 12)
        {
            length = 12;
        }

        var chars = new char[length];

        // Guarantee every complexity class regardless of the random tail.
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);

        for (var i = 4; i < length; i++)
        {
            chars[i] = Pick(All);
        }

        // Fisher-Yates so the guaranteed characters do not sit at fixed positions.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
