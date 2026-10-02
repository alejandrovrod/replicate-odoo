using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Authorization;

/// <summary>
/// Requirement behind the "TenantMember" policy (Constitution Article VI.1). Phase 2 has no
/// authentication task in tasks.md, so the requirement for now is simply "the tenant is resolved":
/// TenantResolutionMiddleware runs before UseAuthorization, and the handler checks
/// <c>ITenantProvider.HasTenant()</c>. <c>RequireAuthenticatedUser()</c> (plus user/tenant-membership
/// checks) is added here when bearer authentication lands in a later phase.
/// </summary>
public sealed class TenantMemberRequirement : IAuthorizationRequirement
{
}
