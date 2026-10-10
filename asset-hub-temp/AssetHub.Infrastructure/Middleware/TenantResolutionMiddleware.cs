using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace AssetHub.Infrastructure.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        var tenant = resolver.GetCurrentTenant();
        
        if (tenant != null && tenant.Status == TenantStatus.Suspended)
        {
            // Solo dejamos pasar requests a billing
            if (!context.Request.Path.StartsWithSegments("/api/v1/billing"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync("{\"code\":\"tenant_suspended\",\"title\":\"Tenant is suspended.\"}");
                return;
            }
        }

        await _next(context);
    }
}
