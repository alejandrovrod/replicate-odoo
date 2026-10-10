using System;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using AssetHub.Infrastructure.Persistence;
using System.Linq;
using System.Security.Claims;

namespace AssetHub.Infrastructure.Tenancy;

public class TenantResolver : ITenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;

    private const string ContextKey = "CurrentTenant";

    public TenantResolver(IHttpContextAccessor httpContextAccessor, IMemoryCache cache, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public Tenant? GetCurrentTenant()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        if (httpContext.Items.TryGetValue(ContextKey, out var tenantObj) && tenantObj is Tenant tenant)
        {
            return tenant;
        }

        // 1. Intentar resolver desde el claim JWT si el usuario está autenticado
        var tidClaim = httpContext.User?.FindFirst("tid")?.Value;
        if (Guid.TryParse(tidClaim, out var tid))
        {
            var cachedTenantById = _cache.GetOrCreate($"TenantId_{tid}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                return db.Tenants.FirstOrDefault(t => t.Id == tid);
            });
            
            if (cachedTenantById != null)
            {
                httpContext.Items[ContextKey] = cachedTenantById;
                return cachedTenantById;
            }
        }

        // 2. Resolucion manual por slug (para endpoints públicos o sin login)
        var slug = ResolveSlug(httpContext);
        if (string.IsNullOrEmpty(slug)) return null;

        var cachedTenant = _cache.GetOrCreate($"Tenant_{slug}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var tenant = db.Tenants.FirstOrDefault(t => t.Slug == slug);
                return tenant;
            }
        });

        if (cachedTenant != null)
        {
            httpContext.Items[ContextKey] = cachedTenant;
        }

        return cachedTenant;
    }

    public Guid? GetCurrentTenantId() => GetCurrentTenant()?.Id;

    public Guid? GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subClaim = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(subClaim, out var userId))
        {
            return userId;
        }

        // Fallback: try 'sub' claim (JWT standard)
        subClaim = httpContext.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(subClaim, out userId))
        {
            return userId;
        }

        return null;
    }

    public static string? ResolveSlug(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant", out var headerVal))
        {
            return headerVal.ToString();
        }

        var host = context.Request.Host.Host;
        if (host.EndsWith(".assethub.app"))
        {
            return host.Split('.')[0];
        }

        return null;
    }
}
