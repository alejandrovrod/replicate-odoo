using Erp.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Middleware;

/// <summary>
/// Resolves the tenant for the current request BEFORE anything else runs (Constitution Article II.2).
/// Sources, in order: the <c>X-Tenant-ID</c> header (must parse as a non-empty GUID), then the
/// <c>tenant_id</c> JWT claim when the header is absent.
/// </summary>
/// <remarks>
/// Tenant-exempt paths (/health, /alive, /openapi*, /swagger*) must never demand a tenant, otherwise
/// Aspire's health checks regress. Any other path without a usable tenant is rejected with RFC 7807
/// ProblemDetails: 400 when the caller is anonymous, 401 when <c>context.User</c> is authenticated.
/// The 401 branch exists for when bearer authentication lands in a later phase - no auth packages
/// are added in Phase 1, but reading Identity/claims costs nothing today.
/// </remarks>
public sealed class TenantResolutionMiddleware
{
    public const string TenantIdHeader = "X-Tenant-ID";
    public const string TenantIdClaim = "tenant_id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        if (IsTenantExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        Guid? tenantId = null;

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
        else if (context.User.FindFirst(TenantIdClaim) is { } tenantClaim
                 && Guid.TryParse(tenantClaim.Value, out var claimTenantId)
                 && claimTenantId != Guid.Empty)
        {
            // Fallback: JWT claim, used only when the header is absent.
            tenantId = claimTenantId;
        }

        if (tenantId is null)
        {
            // Authenticated callers without a tenant get 401 (this branch only becomes reachable
            // once authentication ships in a later phase); anonymous callers get 400 so the client
            // learns the request itself is malformed rather than unauthorized.
            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
            await WriteProblemAsync(
                context,
                isAuthenticated ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest,
                "Tenant Required",
                isAuthenticated
                    ? $"The request is authenticated but no tenant could be resolved. Provide the {TenantIdHeader} header or a '{TenantIdClaim}' claim."
                    : $"No tenant could be resolved. Provide the {TenantIdHeader} header (GUID) or authenticate with a '{TenantIdClaim}' claim.");
            return;
        }

        tenantProvider.SetCurrentTenantId(tenantId.Value);

        await _next(context);
    }

    /// <summary>
    /// Paths that must work without a tenant: Aspire health endpoints (ServiceDefaults
    /// MapDefaultEndpoints) and OpenAPI/Swagger documents.
    /// </summary>
    private static bool IsTenantExempt(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/alive")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/swagger");

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = status switch
            {
                StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path.Value,
        };

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
