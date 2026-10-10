using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Auth.Commands;

public record RegisterViaInvitationCommand(string InvitationToken, string Password, string FullName) : IRequest<bool>;

public class RegisterViaInvitationCommandHandler : IRequestHandler<RegisterViaInvitationCommand, bool>
{
    private readonly ISecurityDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterViaInvitationCommandHandler(
        ISecurityDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> Handle(RegisterViaInvitationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvitationToken))
            throw new ArgumentException("Token de invitación inválido.");

        var invitation = await _db.UserInvitations
            .FirstOrDefaultAsync(i => i.Token == request.InvitationToken, cancellationToken)
            ?? throw new InvalidOperationException("Invitación no encontrada.");

        if (invitation.AcceptedAt != null)
            throw new InvalidOperationException("La invitación ya fue utilizada.");

        if (invitation.CancelledAt != null)
            throw new InvalidOperationException("La invitación fue cancelada.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("La invitación expiró. Pedí que te reenvíen una nueva.");

        var fullName = (request.FullName ?? string.Empty).Trim();
        if (fullName.Length < 2)
            throw new ArgumentException("Ingresá tu nombre completo.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");

        // Email ya registrado como usuario del tenant
        var existing = await _db.Users
            .AnyAsync(u => u.Email == invitation.Email && u.TenantId == invitation.TenantId, cancellationToken);
        if (existing)
            throw new InvalidOperationException("Ya existe un usuario con este email.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = invitation.Email,
            Email = invitation.Email,
            FullName = fullName,
            IsActive = true,
            TenantId = invitation.TenantId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));

        // Rol definido en la invitación
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == invitation.RoleId, cancellationToken);
        if (role != null)
        {
            await _userManager.AddToRoleAsync(user, role.Name!);
        }

        // Vincular empleado existente con el mismo email del tenant (si existe)
        // Nota: los empleados viven en el TenantDbContext; el vínculo se hace
        // desde el lado de Staff (LinkUserToEmployeeCommand) cuando corresponda.

        invitation.AcceptedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = invitation.TenantId,
            UserId = user.Id,
            Action = "user.registered_via_invitation",
            EntityType = "User",
            EntityId = user.Id,
            NewValues = $"email={invitation.Email};role={role?.Name}",
            At = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
