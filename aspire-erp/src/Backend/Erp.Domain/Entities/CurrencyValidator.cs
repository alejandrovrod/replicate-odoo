using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Currency aggregate (RM-09). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is unit-tested
/// without a database (mirrors <see cref="AccountValidator"/>).
/// </summary>
public static class CurrencyValidator
{
    public const int MaxCodeLength = 3;
    public const int MaxSymbolLength = 10;
    public const int MaxFractionNameLength = 50;

    /// <summary>Field-level rules: required ISO code (3), required symbol (10), fraction name (50).</summary>
    /// <exception cref="CurrencyValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(
        string? code,
        string? symbol,
        string? fractionName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new CurrencyValidationException(
                CurrencyErrorCodes.CodeRequired,
                "Currency Code is required.");
        }

        if (code.Trim().Length > MaxCodeLength)
        {
            throw new CurrencyValidationException(
                CurrencyErrorCodes.CodeTooLong,
                $"Currency Code must not exceed {MaxCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new CurrencyValidationException(
                CurrencyErrorCodes.SymbolRequired,
                "Currency Symbol is required.");
        }

        if (symbol.Trim().Length > MaxSymbolLength)
        {
            throw new CurrencyValidationException(
                CurrencyErrorCodes.SymbolTooLong,
                $"Currency Symbol must not exceed {MaxSymbolLength} characters.");
        }

        if (fractionName is { Length: > MaxFractionNameLength })
        {
            throw new CurrencyValidationException(
                CurrencyErrorCodes.FractionNameTooLong,
                $"FractionName must not exceed {MaxFractionNameLength} characters.");
        }
    }
}
