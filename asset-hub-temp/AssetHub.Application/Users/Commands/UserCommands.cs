using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Users.Commands;

/// <summary>
/// Helper de auditoría R3 para acciones de gestión de usuarios.
/// </summary>
internal static class UserAuditHelper
{
    public static void Log(ISecurityDbContext db, Guid? tenantId, Guid? userId,
        string action, Guid entityId, string? newValues = null, string? oldValues = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = entityId,
            NewValues = newValues,
            OldValues = oldValues,
            At = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Crea un usuario del tenant con contraseña y roles.
/// </summary>
public class CreateUserCommand : IRequest<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public System.Collections.Generic.List<Guid> RoleIds { get; set; } = new();
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public CreateUserCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var email = (request.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email es obligatorio.");

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
            throw new ArgumentException($"Ya existe un usuario con el email '{email}'.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = (request.FullName ?? string.Empty).Trim(),
            IsActive = request.IsActive,
            TenantId = tenantId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));

        foreach (var roleId in request.RoleIds.Distinct())
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
            if (role == null)
                throw new ArgumentException($"Rol no encontrado: {roleId}.");
            await _userManager.AddToRoleAsync(user, role.Name!);
        }

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.created", user.Id,
            newValues: $"email={email};name={user.FullName}");

        await _db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}

/// <summary>
/// Actualiza nombre y estado de un usuario del tenant.
/// </summary>
public class UpdateUserCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public UpdateUserCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Usuario no encontrado.");

        var oldValues = $"name={user.FullName};active={user.IsActive}";
        user.FullName = (request.FullName ?? string.Empty).Trim();
        user.IsActive = request.IsActive;

        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new ArgumentException("El nombre es obligatorio.");

        await _userManager.UpdateAsync(user);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.updated", user.Id,
            newValues: $"name={user.FullName};active={user.IsActive}", oldValues: oldValues);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Desactiva (soft delete R8) un usuario del tenant. Requiere reactivación
/// explícita; el login rechaza usuarios inactivos (IsActive check).
/// </summary>
public class DeactivateUserCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
}

public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public DeactivateUserCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Usuario no encontrado.");

        if (user.Id == _currentUser.Id)
            throw new InvalidOperationException("No podés desactivar tu propio usuario.");

        user.IsActive = false;
        await _userManager.UpdateAsync(user);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.deactivated", user.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Reactiva un usuario desactivado.
/// </summary>
public class ActivateUserCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
}

public class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public ActivateUserCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Usuario no encontrado.");

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.activated", user.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Asigna un rol a un usuario del tenant.
/// </summary>
public class AssignUserRoleCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public class AssignUserRoleCommandHandler : IRequestHandler<AssignUserRoleCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public AssignUserRoleCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Usuario no encontrado.");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new ArgumentException("Rol no encontrado.");

        if (await _userManager.IsInRoleAsync(user, role.Name!))
            return Unit.Value; // idempotente

        await _userManager.AddToRoleAsync(user, role.Name!);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.role.assigned", user.Id,
            newValues: $"role={role.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Quita un rol a un usuario del tenant.
/// </summary>
public class RemoveUserRoleCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public class RemoveUserRoleCommandHandler : IRequestHandler<RemoveUserRoleCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public RemoveUserRoleCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _userManager = userManager;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Usuario no encontrado.");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new ArgumentException("Rol no encontrado.");

        if (!await _userManager.IsInRoleAsync(user, role.Name!))
            return Unit.Value; // idempotente

        // No dejar al usuario sin ningún rol
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count == 1 && currentRoles[0] == role.Name)
            throw new InvalidOperationException("No se puede quitar el único rol del usuario.");

        await _userManager.RemoveFromRoleAsync(user, role.Name!);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.role.removed", user.Id,
            newValues: $"role={role.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
