namespace Erp.Application.Common;

/// <summary>
/// Ambient caller roles for operational gates (workflow Fase 5): the role claims of the
/// signed JWT (ClaimTypes.Role + "roles"). Implemented in Erp.Api over HttpContext; the
/// permission system stays the security boundary, roles only steer operational flow.
/// </summary>
/// <remarks>
/// Handlers take it as an OPTIONAL dependency (null = no roles, e.g. legacy direct
/// constructions in unit tests): role-restricted transitions then fail closed with
/// <c>workflow_role_unauthorized</c>, open transitions still pass.
/// </remarks>
public interface ICurrentUserRoles
{
    /// <summary>Role claims of the current caller (empty outside an authenticated request).</summary>
    IReadOnlyCollection<string> Roles { get; }
}
