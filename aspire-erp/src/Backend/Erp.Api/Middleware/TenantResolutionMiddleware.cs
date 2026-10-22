using Erp.Api.Common;
using Erp.Application.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Erp.Api.Middleware;

/// <summary>
/// Resolves the tenant for the current request BEFORE anything else runs (Constitution Article II.2).
/// Sources, in order: the <c>X-Tenant-ID</c> header (developer/testing override, must parse as a
/// non-empty GUID), the host subdomain (<c>{subdomain}.domain.com</c> or
/// <c>{subdomain}.localhost</c>, cached in <see cref="IMemoryCache"/> with a database fallback),
/// then the <c>tenant_id</c> JWT claim.
/// </summary>
/// <remarks>
/// Tenant-exempt paths (/health, /alive, /openapi*, /swagger*, and the three public tenant
/// onboarding endpoints) must never demand a tenant, otherwise health checks and self-service
/// registration regress. Any other path without a usable tenant is rejected with RFC 7807
/// ProblemDetails: 400 when the caller is anonymous, 401 when <c>context.User</c> is
/// authenticated, and 404 when a well-formed host subdomain names no active tenant.
/// </remarks>
public sealed class TenantResolutionMiddleware
{
    public const string TenantIdHeader = "X-Tenant-ID";
    public const string TenantIdClaim = "tenant_id";
    public const string BaseDomainConfigKey = "Tenant:BaseDomain";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        IMemoryCache cache)
    {
        if (IsTenantExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        Guid? tenantId = null;

        // 1. Explicit header override (dev/testing, and the pre-subdomain SPA contract).
        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var headerValues))
        {
            var headerValue = headerValues.ToString().Trim();
            if (!Guid.TryParse(headerValue, out var headerTenantId) || headerTenantId == Guid.Empty)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Invalid Tenant Identifier",
                    $"The {TenantIdHeader} header must be a single non-empty GUID value. Received: '{headerValue}'.");
                return;
            }

            tenantId = headerTenantId;
        }

        // 2. Host-based subdomain resolution with memory cache + DB fallback.
        if (tenantId is null)
        {
            var host = ResolveRequestHost(context.Request);
            var subdomain = TenantSubdomain.ExtractFromHost(
                host,
                _configuration[BaseDomainConfigKey]);

            if (subdomain is not null)
            {
                var cacheKey = TenantSubdomainCache.Key(subdomain);

                if (cache.TryGetValue(cacheKey, out Guid cachedTenantId)
                    && cachedTenantId != Guid.Empty)
                {
                    tenantId = cachedTenantId;
                }
                else
                {
                    // Resolve the lookup in its OWN scope: the request's scoped AppDbContext must
                    // not be constructed before ITenantProvider is pinned, otherwise EF caches the
                    // empty-tenant model for this scope and every later query in the request filters
                    // on the wrong provider instance.
                    using var lookupScope = context.RequestServices.CreateScope();
                    var tenants = lookupScope.ServiceProvider
                        .GetRequiredService<ITenantOnboardingRepository>();
                    var tenant = await tenants.GetBySubdomainAsync(subdomain, context.RequestAborted);

                    if (tenant is null)
                    {
                        await WriteProblemAsync(
                            context,
                            StatusCodes.Status404NotFound,
                            "Tenant Not Found",
                            $"No active tenant is configured for subdomain '{subdomain}'.",
                            TenantErrorCodes.TenantNotFound);
                        return;
                    }

                    cache.Set(
                        cacheKey,
                        tenant.Id,
                        new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TenantSubdomainCache.Ttl,
                        });

                    tenantId = tenant.Id;
                }
            }
        }

        // 3. JWT claim fallback (used only when neither header nor host resolved).
        if (tenantId is null
            && context.User.FindFirst(TenantIdClaim) is { } tenantClaim
            && Guid.TryParse(tenantClaim.Value, out var claimTenantId)
            && claimTenantId != Guid.Empty)
        {
            tenantId = claimTenantId;
        }

        if (tenantId is null)
        {
            // Authenticated callers without a tenant get 401; anonymous callers get 400 so the
            // client learns the request itself is malformed rather than unauthorized.
            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
            await WriteProblemAsync(
                context,
                isAuthenticated ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest,
                "Tenant Required",
                isAuthenticated
                    ? $"The request is authenticated but no tenant could be resolved. Provide the {TenantIdHeader} header, use a tenant subdomain, or authenticate with a '{TenantIdClaim}' claim."
                    : $"No tenant could be resolved. Provide the {TenantIdHeader} header (GUID), use a tenant subdomain, or authenticate with a '{TenantIdClaim}' claim.");
            return;
        }

        tenantProvider.SetCurrentTenantId(tenantId.Value);

        await _next(context);
    }

    /// <summary>
    /// Paths that must work without a tenant: Aspire health endpoints (ServiceDefaults
    /// MapDefaultEndpoints), OpenAPI/Swagger documents, and the public onboarding endpoints.
    /// </summary>
    private static bool IsTenantExempt(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/alive")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/swagger")
        || path.StartsWithSegments("/api/v1/tenants/check-availability")
        || path.StartsWithSegments("/api/v1/tenants/by-subdomain")
        || path.StartsWithSegments("/api/v1/tenants/register");

    /// <summary>
    /// Uses the first <c>X-Forwarded-Host</c> value when a reverse proxy supplies one, otherwise
    /// the Host header. Local Vite runs keep <c>localhost</c> and fall back to the header/JWT.
    /// </summary>
    private static string ResolveRequestHost(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Forwarded-Host", out var forwarded)
            && forwarded.Count > 0)
        {
            var first = forwarded[0]?.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
            {
                return first;
            }
        }

        return request.Host.Host;
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string? code = null)
    {
        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path.Value,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
