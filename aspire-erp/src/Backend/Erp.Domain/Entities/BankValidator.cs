using System.Globalization;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the <see cref="Bank"/> catalog master (bank-catalog spec §3).
/// No EF Core, no NuGet packages - Constitution Article I.2 keeps Erp.Domain dependency-free,
/// so every rule here is unit-tested without a database (mirrors <see cref="CurrencyValidator"/>).
/// </summary>
public static class BankValidator
{
    public const int MaxCountryCodeLength = 2;
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 150;
    public const int MaxSwiftBicLength = 11;

    /// <summary>
    /// Offline ISO 3166-1 alpha-2 whitelist derived from the BCL's own region data
    /// (no network, no packages): every <see cref="RegionInfo.TwoLetterISORegionName"/>
    /// reachable from the installed specific cultures.
    /// </summary>
    private static readonly HashSet<string> IsoCountries = new(
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c =>
            {
                try
                {
                    return new RegionInfo(c.Name).TwoLetterISORegionName;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            })
            .Where(c => c is not null)!,
        StringComparer.Ordinal);

    /// <summary>
    /// Normalizes a country input to uppercase ISO 3166-1 alpha-2 ("mx" -> "MX").
    /// </summary>
    /// <exception cref="BankingValidationException">
    /// <c>invalid_bank_country</c>: missing, wrong length, non-letter, or not an ISO country
    /// (spec BNK-G5: "XX" is rejected).
    /// </exception>
    public static string NormalizeCountryCode(string? countryCode)
    {
        var normalized = countryCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length != MaxCountryCodeLength
            || !normalized.All(c => c is >= 'A' and <= 'Z')
            || !IsoCountries.Contains(normalized))
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidBankCountry,
                $"Country code '{countryCode}' is not a valid ISO 3166-1 alpha-2 code.");
        }

        return normalized;
    }

    /// <summary>
    /// Field-level rules: ISO country (2, normalized uppercase), bank key (required, 20;
    /// MX keys are exactly 3 digits per Banxico/SPEI), official name (required, 150),
    /// SWIFT/BIC (optional, 11).
    /// </summary>
    /// <exception cref="BankingValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(
        string? countryCode,
        string? code,
        string? name,
        string? swiftBic)
    {
        var normalizedCountry = NormalizeCountryCode(countryCode);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidBankCode,
                "Bank Code is required.");
        }

        var trimmedCode = code.Trim();
        if (trimmedCode.Length > MaxCodeLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidBankCode,
                $"Bank Code must not exceed {MaxCodeLength} characters.");
        }

        // MX catalog keys are the 3-digit Banxico/SPEI clave (spec §5); other countries stay
        // generic (required + max length only).
        if (normalizedCountry == "MX"
            && (trimmedCode.Length != 3 || !trimmedCode.All(char.IsDigit)))
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidBankCode,
                $"Mexican bank Code must be exactly 3 digits (received '{code}').");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankNameRequired,
                "Bank Name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankNameRequired,
                $"Bank Name must not exceed {MaxNameLength} characters.");
        }

        if (swiftBic is not null && swiftBic.Trim().Length > MaxSwiftBicLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidBankCode,
                $"SWIFT/BIC must not exceed {MaxSwiftBicLength} characters.");
        }
    }
}
