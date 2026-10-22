namespace Erp.Domain.Entities;

/// <summary>
/// Stable machine-readable failure codes for self-service tenant onboarding and host-based
/// tenant resolution. They flow Domain -&gt; Application (<c>Error.Code</c>) -&gt; Api, where
/// <c>TenantsController</c> maps them to RFC 7807 status codes.
/// </summary>
/// <remarks>
/// Every constant must resolve in <c>ErrorMessages{,.es}.resx</c> (enforced by
/// <c>ErrorCatalogCoverageTests</c>) and is mirrored to the frontend <c>error.json</c> catalogs
/// via <c>scripts/i18n-sync.mjs</c>.
/// </remarks>
public static class TenantErrorCodes
{
    /// <summary>The subdomain does not match the DNS-label rule (400).</summary>
    public const string SubdomainInvalid = "SUBDOMAIN_INVALID";

    /// <summary>The subdomain is reserved by the platform (400).</summary>
    public const string SubdomainReserved = "SUBDOMAIN_RESERVED";

    /// <summary>Another tenant already owns the subdomain (409).</summary>
    public const string SubdomainTaken = "SUBDOMAIN_TAKEN";

    /// <summary>No active tenant resolves for the requested subdomain (404).</summary>
    public const string TenantNotFound = "TENANT_NOT_FOUND";

    /// <summary>Any other onboarding field failed validation (400).</summary>
    public const string InvalidTenantData = "INVALID_TENANT_DATA";
}
