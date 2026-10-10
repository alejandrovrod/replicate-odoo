using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Auth.Commands;

public record UploadAvatarCommand(string AvatarUrl) : IRequest<CurrentUserDto>;

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, CurrentUserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITenantResolver _tenantResolver;

    public UploadAvatarCommandHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        IPlatformDbContext platformDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _platformDb = platformDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<CurrentUserDto> Handle(UploadAvatarCommand request, CancellationToken ct)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("Usuario no autenticado");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            throw new UnauthorizedAccessException("Usuario no encontrado");
        }

        user.AvatarUrl = request.AvatarUrl ?? string.Empty;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Error al actualizar avatar: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await ResolvePermissionsAsync(user, roles, ct);

        string? tenantSlug = null;
        string? tenantName = null;

        if (user.TenantId.HasValue)
        {
            var tenant = await _platformDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, ct);
            if (tenant != null)
            {
                tenantSlug = tenant.Slug;
                tenantName = tenant.Name;
            }
        }

        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roles.ToList(),
            Permissions = permissions,
            TenantSlug = tenantSlug,
            TenantName = tenantName,
            AvatarUrl = user.AvatarUrl,
            PreferredLocale = user.PreferredLocale,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LinkedEmployee = null
        };
    }

    private async Task<List<string>> ResolvePermissionsAsync(ApplicationUser user, IList<string> roles, CancellationToken ct)
    {
        var roleIds = await _securityDb.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roleIds.Count == 0) return new List<string>();

        var query = _securityDb.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId));

        if (user.TenantId.HasValue)
        {
            query = query.Where(rp => rp.TenantId == user.TenantId.Value);
        }

        var permissionIds = await query
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        return await _securityDb.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync(ct);
    }
}