using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssetHub.Infrastructure.Security;

/// <summary>
/// Prefijo de las policies dinamicas de permisos: "permission:assets:read".
/// </summary>
public static class PermissionPolicy
{
    public const string Prefix = "permission:";

    public static string For(string permissionCode) => Prefix + permissionCode;

    public static bool IsPermissionPolicy(string policyName) =>
        policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string PermissionFromPolicy(string policyName) =>
        policyName[Prefix.Length..];
}

/// <summary>
/// Provider de policies dinamicas: crea una policy RequireAuthenticatedUser
/// con el requisito del permiso embebido, sin registrarlas una por una.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.IsPermissionPolicy(policyName))
        {
            var permission = PermissionPolicy.PermissionFromPolicy(policyName);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

/// <summary>
/// Requisito de un permiso especifico en el claim "perms" del JWT.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

/// <summary>
/// Valida el claim "perms" del JWT. El rol "admin" (system) o "Tenant Admin"
/// tienen acceso total como fallback de bootstrap cuando la matriz del tenant
/// aun no fue poblada.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Bootstrap: admins siempre pueden (evita lockout antes de seed)
        var isAdmin = context.User.IsInRole("admin") || context.User.IsInRole("Tenant Admin");
        var perms = context.User.FindAll("perms").Select(c => c.Value).ToHashSet();

        if (isAdmin || perms.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
