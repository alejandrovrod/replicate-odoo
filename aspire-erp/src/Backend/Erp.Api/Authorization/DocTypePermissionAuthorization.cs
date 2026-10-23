using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Erp.Api.Authorization;

public static class DocTypePermissionPolicy
{
    public const string Prefix = "permission:";

    public static string For(string permissionCode) => Prefix + permissionCode;

    public static bool IsPermissionPolicy(string policyName) =>
        policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string PermissionFromPolicy(string policyName) =>
        policyName[Prefix.Length..];
}

public class DocTypePermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider _fallback;

    public DocTypePermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (DocTypePermissionPolicy.IsPermissionPolicy(policyName))
        {
            var permission = DocTypePermissionPolicy.PermissionFromPolicy(policyName);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new DocTypePermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

public class DocTypePermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public DocTypePermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public class DocTypePermissionAuthorizationHandler : AuthorizationHandler<DocTypePermissionRequirement>
{
    private readonly Erp.Domain.Repositories.IUserRepository _userRepository;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public DocTypePermissionAuthorizationHandler(
        Erp.Domain.Repositories.IUserRepository userRepository,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocTypePermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Bootstrap: System Manager (ERPNext equivalent to Admin) has all access
        if (context.User.IsInRole("System Manager"))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var cacheKey = $"user_perms_{userId}";
        if (!_cache.TryGetValue(cacheKey, out HashSet<string>? perms))
        {
            var dbPerms = await _userRepository.GetUserPermissionsAsync(userId);
            perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in dbPerms)
            {
                var dt = p.DocType.ToLowerInvariant().Replace(" ", "_");
                if (p.CanRead) perms.Add($"{dt}:read");
                if (p.CanWrite) perms.Add($"{dt}:write");
                if (p.CanCreate) perms.Add($"{dt}:create");
                if (p.CanDelete) perms.Add($"{dt}:delete");
                if (p.CanSubmit) perms.Add($"{dt}:submit");
                if (p.CanCancel) perms.Add($"{dt}:cancel");
            }

            _cache.Set(cacheKey, perms, TimeSpan.FromMinutes(5));
        }

        if (perms != null && perms.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
