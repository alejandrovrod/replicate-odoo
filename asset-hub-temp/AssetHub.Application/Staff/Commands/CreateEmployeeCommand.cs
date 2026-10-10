using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using AssetHub.Domain.Staff;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using AssetHub.Application.Staff.Helpers;

namespace AssetHub.Application.Staff.Commands;

public class CreateEmployeeResult
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? TemporalPassword { get; set; }
}

public class CreateEmployeeCommand : IRequest<CreateEmployeeResult>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PreferredLocale { get; set; }
    public Guid RoleCatalogItemId { get; set; }
    public Guid[] Skills { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Cuando es true, se crea el ApplicationUser en Identity con el
    /// SystemRoleId seleccionado y se vincula al empleado (UserId).
    /// </summary>
    public bool CreateUserAccess { get; set; }

    /// <summary>
    /// Rol de sistema (Identity, tabla Roles) asignado al usuario creado.
    /// Requerido cuando CreateUserAccess es true.
    /// </summary>
    public Guid? SystemRoleId { get; set; }
}

public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, CreateEmployeeResult>
{
    private readonly ITenantDbContext _db;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateEmployeeCommandHandler(
        ITenantDbContext db,
        ISecurityDbContext securityDb,
        ITenantResolver tenantResolver,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _securityDb = securityDb;
        _tenantResolver = tenantResolver;
        _userManager = userManager;
    }

    public async Task<CreateEmployeeResult> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        // Ensure role catalog exists for this tenant (lazy init)
        await StaffCatalogDefaults.EnsureRoleCatalogAsync(_db, tenantId, cancellationToken);

        var roleExists = await _db.CatalogItems.AnyAsync(ci => ci.Id == request.RoleCatalogItemId, cancellationToken);
        if (!roleExists)
            throw new ArgumentException("Role catalog item not found");

        // Validación anticipada del rol de sistema (antes de tocar Identity):
        // si falla aquí, no se crea ni el empleado ni el usuario.
        ApplicationRole? systemRole = null;
        if (request.CreateUserAccess)
        {
            if (request.SystemRoleId is null || request.SystemRoleId == Guid.Empty)
                throw new ArgumentException("SystemRoleId es requerido cuando se crea acceso al sistema.");

            systemRole = await _securityDb.Roles
                .FirstOrDefaultAsync(r => r.Id == request.SystemRoleId, cancellationToken);
            if (systemRole == null)
                throw new ArgumentException("Rol de sistema no encontrado.");
        }

        // Creación del usuario en Identity (si corresponde).
        // UserManager persiste en su propio DbContext (SecurityDbContext); si la
        // creación del empleado falla después, se elimina el usuario por compensación
        // para no dejar accesos huérfanos.
        ApplicationUser? createdUser = null;
        string? temporalPassword = null;

        if (request.CreateUserAccess)
        {
            var email = (request.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("El email es obligatorio para crear acceso al sistema.");

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
                throw new ArgumentException($"Ya existe un usuario con el email '{email}'.");

            temporalPassword = GenerateTemporalPassword();

            createdUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                TenantId = tenantId,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(createdUser, temporalPassword);
            if (!result.Succeeded)
                throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));

            var roleResult = await _userManager.AddToRoleAsync(createdUser, systemRole!.Name!);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(createdUser);
                throw new ArgumentException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        var emp = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PreferredLocale = string.IsNullOrWhiteSpace(request.PreferredLocale) ? "es" : request.PreferredLocale.Trim(),
            RoleCatalogItemId = request.RoleCatalogItemId,
            SkillsJson = JsonSerializer.Serialize(request.Skills),
            UserId = createdUser?.Id
        };

        _db.Employees.Add(emp);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensación: falló el empleado -> revertir el usuario de Identity.
            if (createdUser != null)
            {
                await _userManager.DeleteAsync(createdUser);
            }
            throw;
        }

        return new CreateEmployeeResult
        {
            Id = emp.Id,
            UserId = createdUser?.Id,
            TemporalPassword = createdUser != null ? temporalPassword : null
        };
    }

    /// <summary>
    /// Contraseña temporal segura (no paso por PasswordValidator de Identity
    /// porque este pipeline es interno: mezcla de segmentos aleatorios).
    /// </summary>
    private static string GenerateTemporalPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%";

        var random = new Random();
        char Pick(string set) => set[random.Next(set.Length)];

        var chars = new[]
        {
            Pick(upper), Pick(upper),
            Pick(lower), Pick(lower), Pick(lower), Pick(lower), Pick(lower),
            Pick(digits), Pick(digits), Pick(digits),
            Pick(special)
        };

        // Shuffle Fisher-Yates
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
