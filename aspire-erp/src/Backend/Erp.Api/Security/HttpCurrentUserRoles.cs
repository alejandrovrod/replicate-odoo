using System.Security.Claims;
using Erp.Application.Common;

namespace Erp.Api.Security;

/// <summary>
/// Ambient caller roles from the signed JWT (workflow Fase 5): <c>ClaimTypes.Role</c> plus
/// the duplicated <c>"roles"</c> claim emitted by the token generator. Empty outside an
/// authenticated request (handlers fail role-restricted transitions closed in that case).
/// </summary>
public sealed class HttpCurrentUserRoles : ICurrentUserRoles
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserRoles(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return Array.Empty<string>();
            }

            return user
                .FindAll(ClaimTypes.Role)
                .Concat(user.FindAll("roles"))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
