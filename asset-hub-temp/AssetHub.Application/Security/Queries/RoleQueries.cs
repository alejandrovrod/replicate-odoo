using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Security.Queries;

/// <summary>
/// Lista los roles con la cantidad de permisos asignados al tenant actual.
/// </summary>
public class GetRolesQuery : IRequest<List<RoleSummaryDto>>
{
}

public class RoleSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemDefault { get; set; }
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
}

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, List<RoleSummaryDto>>
{
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public GetRolesQueryHandler(ISecurityDbContext securityDb, ITenantResolver tenantResolver)
    {
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<List<RoleSummaryDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var permissionCounts = await _securityDb.RolePermissions
            .Where(rp => rp.TenantId == tenantId)
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var userCounts = await _securityDb.UserRoles
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Distinct().Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var roles = await _securityDb.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleSummaryDto
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
            Description = r.Description,
            IsSystemDefault = r.IsSystemDefault,
            PermissionCount = permissionCounts.TryGetValue(r.Id, out var count) ? count : 0,
            UserCount = userCounts.TryGetValue(r.Id, out var users) ? users : 0
        }).ToList();
    }
}

/// <summary>
/// Detalle de un rol con sus permisos (codigos) para el tenant actual.
/// </summary>
public class GetRoleByIdQuery : IRequest<RoleDetailDto?>
{
    public Guid RoleId { get; set; }
}

public class RoleDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemDefault { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
}

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDetailDto?>
{
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public GetRoleByIdQueryHandler(ISecurityDbContext securityDb, ITenantResolver tenantResolver)
    {
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<RoleDetailDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var role = await _securityDb.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);
        if (role == null) return null;

        var codes = await _securityDb.RolePermissions
            .Where(rp => rp.TenantId == tenantId && rp.RoleId == role.Id)
            .Join(_securityDb.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p.Code)
            .ToListAsync(cancellationToken);

        return new RoleDetailDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Description = role.Description,
            IsSystemDefault = role.IsSystemDefault,
            PermissionCodes = codes
        };
    }
}

/// <summary>
/// Catalogo global de permisos agrupado por modulo funcional.
/// </summary>
public class GetPermissionCatalogQuery : IRequest<List<PermissionCatalogGroupDto>>
{
}

public class PermissionCatalogGroupDto
{
    public string Module { get; set; } = string.Empty;
    public List<PermissionCatalogItemDto> Permissions { get; set; } = new();
}

public class PermissionCatalogItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PlanModule { get; set; } = string.Empty;
}

public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, List<PermissionCatalogGroupDto>>
{
    private readonly ISecurityDbContext _securityDb;

    public GetPermissionCatalogQueryHandler(ISecurityDbContext securityDb)
    {
        _securityDb = securityDb;
    }

    public async Task<List<PermissionCatalogGroupDto>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _securityDb.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return permissions
            .GroupBy(p => p.Module)
            .Select(g => new PermissionCatalogGroupDto
            {
                Module = g.Key,
                Permissions = g.Select(p => new PermissionCatalogItemDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description,
                    PlanModule = p.PlanModule
                }).ToList()
            })
            .ToList();
    }
}
