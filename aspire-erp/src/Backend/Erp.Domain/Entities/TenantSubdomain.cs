using System.Net;
using System.Text.RegularExpressions;
using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure subdomain invariants for tenant onboarding and host-based resolution.
/// No EF Core and no NuGet packages: the rules are unit-testable without a database
/// (Constitution Article I.2).
/// </summary>
public static class TenantSubdomain
{
    /// <summary>Physical column length (<c>Tenant.Subdomain NVARCHAR(64)</c>).</summary>
    public const int MaxColumnLength = 64;

    /// <summary>
    /// Authoritative DNS-label shape: 3..32 chars, lowercase alphanumeric plus hyphens,
    /// never starting or ending with a hyphen.
    /// </summary>
    public static readonly Regex Pattern = new(
        "^[a-z0-9][a-z0-9-]{1,30}[a-z0-9]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    /// <summary>Names the platform owns; tenants can never register them.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(
        new[]
        {
            "api", "app", "admin", "auth", "login", "signup", "register",
            "www", "mail", "status", "dev", "staging",
        },
        StringComparer.Ordinal);

    /// <summary>Trim + lowercase, so storage and comparisons stay deterministic.</summary>
    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>True when the value matches the DNS-label rule (reserved names still pass).</summary>
    public static bool IsFormatValid(string? value) => Pattern.IsMatch(Normalize(value));

    /// <summary>True when the normalized value is reserved by the platform.</summary>
    public static bool IsReserved(string? value) => Reserved.Contains(Normalize(value));

    /// <summary>True when the value is a legal, non-reserved subdomain.</summary>
    public static bool IsValid(string? value) =>
        IsFormatValid(value) && !IsReserved(value);

    /// <summary>
    /// Validates and returns the normalized subdomain. Invalid shape is
    /// <c>SUBDOMAIN_INVALID</c>; a reserved name is <c>SUBDOMAIN_RESERVED</c> (both 400).
    /// </summary>
    /// <exception cref="AuthValidationException">The subdomain is invalid or reserved.</exception>
    public static string EnsureValid(string? value)
    {
        var normalized = Normalize(value);

        if (!Pattern.IsMatch(normalized))
        {
            throw new AuthValidationException(
                TenantErrorCodes.SubdomainInvalid,
                "Subdomain must be 3-32 characters, use lowercase letters, digits or hyphens, "
                + "and must start and end with a letter or digit.");
        }

        if (Reserved.Contains(normalized))
        {
            throw new AuthValidationException(
                TenantErrorCodes.SubdomainReserved,
                $"The subdomain '{normalized}' is reserved by the platform.");
        }

        return normalized;
    }

    /// <summary>
    /// Extracts the tenant label from a request host. Supports
    /// <c>{subdomain}.domain.com</c> (the configured base domain) and
    /// <c>{subdomain}.localhost</c>; returns null for bare hosts, IP addresses, nested labels,
    /// malformed labels and reserved names.
    /// </summary>
    public static string? ExtractFromHost(string? host, string? baseDomain = null)
    {
        var value = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

        // Request.Host.Host already excludes the port; Uri-style inputs (tests, reverse proxies)
        // may still carry one. Only strip a single trailing :port, never an IPv6 literal.
        var colon = value.LastIndexOf(':');
        if (colon > 0 && value.IndexOf(':') == colon)
        {
            value = value[..colon];
        }

        if (value.Length == 0 || IPAddress.TryParse(value, out _))
        {
            return null;
        }

        string? candidate = null;

        if (value.EndsWith(".localhost", StringComparison.Ordinal))
        {
            candidate = value[..^".localhost".Length];
        }
        else
        {
            var baseValue = Normalize(baseDomain is { Length: > 0 } ? baseDomain : "domain.com");
            var suffix = "." + baseValue;

            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                candidate = value[..^suffix.Length];
            }
        }

        if (candidate is null || candidate.Length == 0 || candidate.Contains('.'))
        {
            return null;
        }

        return IsValid(candidate) ? candidate : null;
    }
}
