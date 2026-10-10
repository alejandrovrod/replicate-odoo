using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocTypePermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Bootstrap: System Manager (ERPNext equivalent to Admin) has all access
        var isSystemManager = context.User.IsInRole("System Manager");
        var perms = context.User.FindAll("perms").Select(c => c.Value).ToHashSet();

        if (isSystemManager || perms.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
