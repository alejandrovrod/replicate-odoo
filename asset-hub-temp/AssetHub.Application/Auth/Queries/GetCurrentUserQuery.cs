using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Auth.Queries;

public record GetCurrentUserQuery() : IRequest<CurrentUserDto?>;

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public string? TenantSlug { get; set; }
    public string? TenantName { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string PreferredLocale { get; set; } = "es";
    public bool TwoFactorEnabled { get; set; }
    public EmployeeLinkDto? LinkedEmployee { get; set; }
}

public class EmployeeLinkDto
{
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITenantDbContext _tenantDb;
    private readonly ITenantResolver _tenantResolver;

    public GetCurrentUserQueryHandler(
        UserManager<ApplicationUser> userManager,
        ISecurityDbContext securityDb,
        IPlatformDbContext platformDb,
        ITenantDbContext tenantDb,
        ITenantResolver tenantResolver)
    {
        _userManager = userManager;
        _securityDb = securityDb;
        _platformDb = platformDb;
        _tenantDb = tenantDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _tenantResolver.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            return null;
        }

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

        // Get linked employee if exists
        EmployeeLinkDto? linkedEmployee = null;
        if (user.TenantId.HasValue)
        {
            var employee = await _tenantDb.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == user.Id && e.TenantId == user.TenantId.Value, cancellationToken);
            
            if (employee != null)
            {
                // Get role label from catalog translations
                string roleLabel = string.Empty;
                if (employee.RoleCatalogItemId != Guid.Empty)
                {
                    var roleItem = await _tenantDb.CatalogItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ci => ci.Id == employee.RoleCatalogItemId, cancellationToken);
                    
                    if (roleItem != null)
                    {
                        var translation = await _tenantDb.CatalogItemTranslations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.CatalogItemId == roleItem.Id && t.Locale == user.PreferredLocale, cancellationToken);
                        roleLabel = translation?.Label ?? roleItem.Code;
                    }
                }

                linkedEmployee = new EmployeeLinkDto
                {
                    EmployeeId = employee.Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Email = employee.Email,
                    RoleLabel = roleLabel,
                    PhoneNumber = employee.PhoneNumber ?? string.Empty
                };
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
            LinkedEmployee = linkedEmployee
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