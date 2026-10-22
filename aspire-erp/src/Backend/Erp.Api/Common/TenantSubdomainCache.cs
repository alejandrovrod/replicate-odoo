using Erp.Domain.Entities;

namespace Erp.Api.Common;

/// <summary>
/// Single source of truth for the host-subdomain cache key/TTL. The middleware fills it and the
/// registration endpoint invalidates it, so a newly created tenant is resolvable immediately
/// instead of waiting for the TTL.
/// </summary>
public static class TenantSubdomainCache
{
    private const string Prefix = "tenant:subdomain:";

    /// <summary>Short TTL: tenant deactivation should stop resolving promptly.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static string Key(string subdomain) =>
        Prefix + TenantSubdomain.Normalize(subdomain);
}
