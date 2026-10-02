using Erp.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Authorization;

/// <summary>
/// Succeeds when a tenant was resolved for the current request. A requirement-only policy needs no
/// authentication scheme registration: with only <c>RequireAssertion</c>-style requirements,
/// <c>[Authorize(Policy = "TenantMember")]</c> evaluates without <c>AddAuthentication</c> (verified
/// empirically in Phase 2), so no auth NuGet packages are added yet.
/// </summary>
public sealed class TenantMemberHandler : AuthorizationHandler<TenantMemberRequirement>
{
    private readonly ITenantProvider _tenantProvider;

    public TenantMemberHandler(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantMemberRequirement requirement)
    {
        if (_tenantProvider.HasTenant())
        {
            context.Succeed(requirement);
        }

        // When no tenant is resolved the requirement is not met - however TenantResolutionMiddleware
        // already answered such requests with 400 ProblemDetails before authorization runs.
        return Task.CompletedTask;
    }
}
