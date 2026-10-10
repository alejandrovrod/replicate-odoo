using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Auth.Commands;

public record UpdateCurrentUserCommand(
    [Required] string FullName,
    [Required, EmailAddress] string Email,
    string? PreferredLocale = null
) : IRequest<CurrentUserDto>;

public class UpdateCurrentUserCommandHandler : IRequestHandler<UpdateCurrentUserCommand, CurrentUserDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITenantResolver _tenantResolver;

    public UpdateCurrentUserCommandHandler(
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

    public async Task<CurrentUserDto> Handle(UpdateCurrentUserCommand request, CancellationToken cancellationToken)
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

        // Check email uniqueness within tenant
        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.TenantId == user.TenantId)
            {
                throw new InvalidOperationException("El email ya está en uso por otro usuario en este tenant");
            }
            
            user.Email = request.Email;
            user.UserName = request.Email; // Email is also username
            user.NormalizedEmail = request.Email.ToUpperInvariant();
            user.NormalizedUserName = request.Email.ToUpperInvariant();
        }

        user.FullName = request.FullName;
        
        if (!string.IsNullOrWhiteSpace(request.PreferredLocale))
        {
            user.PreferredLocale = request.PreferredLocale;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Error al actualizar usuario: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Return updated user info
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await ResolvePermissionsAsync(user, roles, cancellationToken);

        string? tenantSlug = null;
        string? tenantName = null;

        if (user.TenantId.HasValue)
        {
            var tenant = await _platformDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, cancellationToken);
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
            LinkedEmployee = null // Will be loaded separately if needed
        };
    }

    private async Task<List<string>> ResolvePermissionsAsync(ApplicationUser user, IList<string> roles, CancellationToken cancellationToken)
    {
        var roleIds = await _securityDb.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

        return await _securityDb.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
    }
}