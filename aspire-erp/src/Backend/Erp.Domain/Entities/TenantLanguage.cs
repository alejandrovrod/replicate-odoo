using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Supported default languages for a tenant workspace. The value drives the language used to
/// seed starter master data and the language the SPA boots with on a tenant subdomain.
/// </summary>
/// <remarks>
/// Pure domain rule: no EF Core and no external packages (Constitution Article I.2). Persisted as
/// <c>Tenant.DefaultLanguage NVARCHAR(5)</c>; only the stable ISO 639-1 codes <c>en</c> and
/// <c>es</c> are accepted.
/// </remarks>
public static class TenantLanguage
{
    public const string English = "en";
    public const string Spanish = "es";

    /// <summary>Default used by the database and by onboarding when the caller omits a value.</summary>
    public const string Default = Spanish;

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(
        [English, Spanish],
        StringComparer.Ordinal);

    /// <summary>Trim + lowercase, so storage and comparisons stay deterministic.</summary>
    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsSupported(string? value) => Supported.Contains(Normalize(value));

    /// <summary>Returns <c>en</c> or <c>es</c>; unsupported values fall back to the default.</summary>
    public static string Resolve(string? value) =>
        Normalize(value) == English ? English : Spanish;

    /// <summary>Validates and returns the normalized language code.</summary>
    /// <exception cref="AuthValidationException">The value is not <c>en</c> or <c>es</c>.</exception>
    public static string EnsureValid(string? value)
    {
        var normalized = Normalize(value);

        if (!Supported.Contains(normalized))
        {
            throw new AuthValidationException(
                TenantErrorCodes.InvalidTenantData,
                "Default language must be either 'en' or 'es'.");
        }

        return normalized;
    }
}
