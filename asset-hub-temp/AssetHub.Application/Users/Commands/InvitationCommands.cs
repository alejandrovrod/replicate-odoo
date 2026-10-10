using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Users.Commands;

/// <summary>
/// Crea una invitación de usuario con rol, token criptográfico y expiración
/// de 72hs. El invitado define su contraseña al aceptar (no se envía).
/// </summary>
public class InviteUserCommand : IRequest<InvitationCreatedResult>
{
    public string Email { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
}

public record InvitationCreatedResult(Guid Id, string Token);

public class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, InvitationCreatedResult>
{
    private const int ExpirationHours = 72;

    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public InviteUserCommandHandler(
        ISecurityDbContext db,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<InvitationCreatedResult> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Ingresá un email válido.");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new ArgumentException("Rol no encontrado.");

        // No invitar emails que ya son usuarios activos del sistema
        var existingUser = await _db.Users
            .AnyAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);
        if (existingUser)
            throw new ArgumentException($"Ya existe un usuario con el email '{email}' en el tenant.");

        // Solo una invitación PENDIENTE por email (cancela las previas)
        var pending = await _db.UserInvitations
            .Where(i => i.TenantId == tenantId && i.Email == email && i.AcceptedAt == null && i.CancelledAt == null)
            .ToListAsync(cancellationToken);
        foreach (var p in pending)
        {
            p.CancelledAt = DateTime.UtcNow;
        }

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            RoleId = role.Id,
            Token = GenerateToken(),
            ExpiresAt = DateTime.UtcNow.AddHours(ExpirationHours),
            CreatedBy = _currentUser.Id ?? Guid.Empty
        };

        _db.UserInvitations.Add(invitation);

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.invited", invitation.Id,
            newValues: $"email={email};role={role.Name}");

        await _db.SaveChangesAsync(cancellationToken);

        return new InvitationCreatedResult(invitation.Id, invitation.Token);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

/// <summary>
/// Reenvía una invitación: nuevo token y expiración fresca (72hs).
/// Devuelve el token nuevo para que el invitador comparta el link.
/// </summary>
public class ResendInvitationCommand : IRequest<InvitationCreatedResult>
{
    public Guid InvitationId { get; set; }
}

public class ResendInvitationCommandHandler : IRequestHandler<ResendInvitationCommand, InvitationCreatedResult>
{
    private const int ExpirationHours = 72;

    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public ResendInvitationCommandHandler(
        ISecurityDbContext db,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<InvitationCreatedResult> Handle(ResendInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var invitation = await _db.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId && i.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Invitación no encontrada.");

        if (invitation.AcceptedAt != null)
            throw new InvalidOperationException("La invitación ya fue aceptada.");

        var bytes = RandomNumberGenerator.GetBytes(32);
        invitation.Token = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        invitation.ExpiresAt = DateTime.UtcNow.AddHours(ExpirationHours);
        invitation.CancelledAt = null;

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.invitation.resent", invitation.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return new InvitationCreatedResult(invitation.Id, invitation.Token);
    }
}

/// <summary>
/// Cancela una invitación pendiente.
/// </summary>
public class CancelInvitationCommand : IRequest<Unit>
{
    public Guid InvitationId { get; set; }
}

public class CancelInvitationCommandHandler : IRequestHandler<CancelInvitationCommand, Unit>
{
    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ICurrentUser _currentUser;

    public CancelInvitationCommandHandler(
        ISecurityDbContext db,
        ITenantResolver tenantResolver,
        ICurrentUser currentUser)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var invitation = await _db.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId && i.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Invitación no encontrada.");

        if (invitation.AcceptedAt != null)
            throw new InvalidOperationException("La invitación ya fue aceptada.");

        invitation.CancelledAt = DateTime.UtcNow;

        UserAuditHelper.Log(_db, tenantId, _currentUser.Id, "user.invitation.cancelled", invitation.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
