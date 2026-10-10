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

namespace AssetHub.Application.Security.Commands;

/// <summary>
/// Helpers compartidos por los handlers de roles: resolucion de permisos del
/// catalogo, validacion R-ROLE-4 (modulo habilitado en plan del tenant),
/// asignacion con auditoria (R3).
/// </summary>
internal static class RolePermissionHelper
{
    public static async Task<Guid> GetRolesManagePermissionIdAsync(ISecurityDbContext db, CancellationToken ct)
    {
        return await db.Permissions
            .Where(p => p.Code == "roles:manage")
            .Select(p => p.Id)
            .FirstAsync(ct);
    }

    /// <summary>
    /// R-ROLE-4: valida que todos los codigos existan en el catalogo global y
    /// que los permisos con PlanModule requieran un modulo habilitado en el
    /// plan del tenant (o que el tenant no tenga plan = acceso completo).
    /// </summary>
    public static async Task<(List<Permission> Permissions, List<string> Errors)> ResolveAndValidateAsync(
        ISecurityDbContext securityDb,
        IPlatformDbContext platformDb,
        Guid tenantId,
        IEnumerable<string> codes,
        CancellationToken ct)
    {
        var codeList = (codes ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        var permissions = await securityDb.Permissions
            .Where(p => codeList.Contains(p.Code))
            .ToListAsync(ct);

        var found = permissions.Select(p => p.Code).ToHashSet();
        var errors = codeList.Where(c => !found.Contains(c)).ToList();

        // Validar plan del tenant
        var tenant = await platformDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        var enabledModules = tenant?.PlanId == null
            ? null // sin plan => sin restricciones (demo/seed)
            : ParseEnabledModules(await platformDb.Plans
                .AsNoTracking()
                .Where(pl => pl.Id == tenant.PlanId)
                .Select(pl => pl.EnabledModules)
                .FirstOrDefaultAsync(ct));

        if (enabledModules != null)
        {
            var missingModules = permissions
                .Where(p => !string.IsNullOrEmpty(p.PlanModule) && !enabledModules.Contains(p.PlanModule))
                .GroupBy(p => p.PlanModule)
                .ToList();

            foreach (var group in missingModules)
            {
                errors.Add($"El plan actual no tiene habilitado el módulo '{group.Key}', requerido por {group.Count()} permiso(s) seleccionado(s).");
            }
        }

        permissions = permissions
            .Where(p => string.IsNullOrEmpty(p.PlanModule) || enabledModules == null || enabledModules.Contains(p.PlanModule))
            .ToList();

        return (permissions, errors);
    }

    /// <summary>
    /// Reemplaza el set completo de permisos de un rol para el tenant y
    /// registra el diff en PermissionAssignmentAudits.
    /// </summary>
    public static async Task ReplaceRolePermissionsAsync(
        ISecurityDbContext securityDb,
        Guid tenantId,
        Guid roleId,
        Guid adminUserId,
        List<Permission> newPermissions,
        CancellationToken ct)
    {
        var existing = await securityDb.RolePermissions
            .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId)
            .ToListAsync(ct);

        var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();
        var newIds = newPermissions.Select(p => p.Id).ToHashSet();

        var removed = existing.Where(rp => !newIds.Contains(rp.PermissionId)).ToList();
        var added = newPermissions.Where(p => !existingIds.Contains(p.Id)).ToList();

        if (removed.Count > 0)
            securityDb.RolePermissions.RemoveRange(removed);

        foreach (var perm in added)
        {
            securityDb.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = perm.Id
            });
        }

        foreach (var rp in removed)
        {
            securityDb.PermissionAssignmentAudits.Add(new PermissionAssignmentAudit
            {
                TenantId = tenantId,
                AdminUserId = adminUserId,
                RoleId = roleId,
                PermissionId = rp.PermissionId,
                Granted = false,
                At = DateTime.UtcNow
            });
        }

        foreach (var perm in added)
        {
            securityDb.PermissionAssignmentAudits.Add(new PermissionAssignmentAudit
            {
                TenantId = tenantId,
                AdminUserId = adminUserId,
                RoleId = roleId,
                PermissionId = perm.Id,
                Granted = true,
                At = DateTime.UtcNow
            });
        }
    }

    private static HashSet<string> ParseEnabledModules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new HashSet<string>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet();
        }
        catch
        {
            return new HashSet<string>();
        }
    }
}

/// <summary>
/// Crea un rol custom con un set inicial de permisos (valida R-ROLE-4).
/// </summary>
public class CreateRoleCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PermissionCodes { get; set; } = new();
}

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITenantResolver _tenantResolver;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUser _currentUser;

    public CreateRoleCommandHandler(
        ISecurityDbContext securityDb,
        IPlatformDbContext platformDb,
        ITenantResolver tenantResolver,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUser currentUser)
    {
        _securityDb = securityDb;
        _platformDb = platformDb;
        _tenantResolver = tenantResolver;
        _roleManager = roleManager;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length < 2)
            throw new ArgumentException("El nombre del rol es obligatorio (mínimo 2 caracteres).");

        var normalized = name.ToUpperInvariant();
        if (await _securityDb.Roles.AnyAsync(r => r.NormalizedName == normalized, cancellationToken))
            throw new ArgumentException($"Ya existe un rol llamado '{name}'.");

        var (permissions, errors) = await RolePermissionHelper.ResolveAndValidateAsync(
            _securityDb, _platformDb, tenantId, request.PermissionCodes, cancellationToken);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalized,
            Description = request.Description ?? string.Empty,
            IsSystemDefault = false,
            TenantId = null // roles globales; la matriz es por tenant via RolePermissions
        };

        var identityResult = await _roleManager.CreateAsync(role);
        if (!identityResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", identityResult.Errors.Select(e => e.Description)));

        var adminUserId = _currentUser.Id ?? Guid.Empty;
        await RolePermissionHelper.ReplaceRolePermissionsAsync(
            _securityDb, tenantId, role.Id, adminUserId, permissions, cancellationToken);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}

/// <summary>
/// Actualiza un rol: renombra, cambia descripcion y reemplaza el set completo
/// de permisos del tenant. R-ROLE-3: no permite dejar al tenant sin ningun
/// rol con roles:manage.
/// </summary>
public class UpdateRoleCommand : IRequest<Unit>
{
    public Guid RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PermissionCodes { get; set; } = new();
}

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Unit>
{
    private readonly ISecurityDbContext _securityDb;
    private readonly IPlatformDbContext _platformDb;
    private readonly ITenantResolver _tenantResolver;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUser _currentUser;

    public UpdateRoleCommandHandler(
        ISecurityDbContext securityDb,
        IPlatformDbContext platformDb,
        ITenantResolver tenantResolver,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUser currentUser)
    {
        _securityDb = securityDb;
        _platformDb = platformDb;
        _tenantResolver = tenantResolver;
        _roleManager = roleManager;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var role = await _securityDb.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new ArgumentException("Rol no encontrado.");

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length < 2)
            throw new ArgumentException("El nombre del rol es obligatorio (mínimo 2 caracteres).");

        var normalized = name.ToUpperInvariant();
        if (await _securityDb.Roles.AnyAsync(r => r.NormalizedName == normalized && r.Id != role.Id, cancellationToken))
            throw new ArgumentException($"Ya existe un rol llamado '{name}'.");

        var (permissions, errors) = await RolePermissionHelper.ResolveAndValidateAsync(
            _securityDb, _platformDb, tenantId, request.PermissionCodes, cancellationToken);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        // R-ROLE-3: solo si el rol HOY tiene roles:manage y el nuevo set lo
        // quita, otro rol del tenant debe conservarlo.
        var rolesManageId = await RolePermissionHelper.GetRolesManagePermissionIdAsync(_securityDb, cancellationToken);
        var roleCurrentlyHasRolesManage = await _securityDb.RolePermissions
            .AnyAsync(rp => rp.TenantId == tenantId && rp.RoleId == role.Id && rp.PermissionId == rolesManageId, cancellationToken);

        if (roleCurrentlyHasRolesManage && !permissions.Any(p => p.Code == "roles:manage"))
        {
            var otherRoleHasIt = await _securityDb.RolePermissions
                .AnyAsync(rp => rp.TenantId == tenantId
                                && rp.PermissionId == rolesManageId
                                && rp.RoleId != role.Id, cancellationToken);
            if (!otherRoleHasIt)
                throw new InvalidOperationException("No se puede quitar roles:manage: sería el último rol del tenant con ese permiso.");
        }

        role.Name = name;
        role.NormalizedName = normalized;
        role.Description = request.Description ?? string.Empty;
        await _roleManager.UpdateAsync(role);

        var adminUserId = _currentUser.Id ?? Guid.Empty;
        await RolePermissionHelper.ReplaceRolePermissionsAsync(
            _securityDb, tenantId, role.Id, adminUserId, permissions, cancellationToken);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Elimina un rol custom (no system default). R-ROLE-3: si el rol es el
/// ultimo con roles:manage, se rechaza. Solo afecta la matriz del tenant.
/// </summary>
public class DeleteRoleCommand : IRequest<Unit>
{
    public Guid RoleId { get; set; }
}

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Unit>
{
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;

    public DeleteRoleCommandHandler(ISecurityDbContext securityDb, ITenantResolver tenantResolver)
    {
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
    }

    public async Task<Unit> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var role = await _securityDb.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new ArgumentException("Rol no encontrado.");

        if (role.IsSystemDefault)
            throw new InvalidOperationException("No se puede eliminar un rol base del sistema.");

        // R-ROLE-3
        var rolesManageId = await RolePermissionHelper.GetRolesManagePermissionIdAsync(_securityDb, cancellationToken);
        var roleHasIt = await _securityDb.RolePermissions
            .AnyAsync(rp => rp.TenantId == tenantId && rp.RoleId == role.Id && rp.PermissionId == rolesManageId, cancellationToken);
        if (roleHasIt)
        {
            var otherRoleHasIt = await _securityDb.RolePermissions
                .AnyAsync(rp => rp.TenantId == tenantId
                                && rp.PermissionId == rolesManageId
                                && rp.RoleId != role.Id, cancellationToken);
            if (!otherRoleHasIt)
                throw new InvalidOperationException("No se puede eliminar el último rol del tenant con roles:manage.");
        }

        var rolePerms = await _securityDb.RolePermissions
            .Where(rp => rp.TenantId == tenantId && rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _securityDb.RolePermissions.RemoveRange(rolePerms);

        _securityDb.Roles.Remove(role);
        await _securityDb.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
