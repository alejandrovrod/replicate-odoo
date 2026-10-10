using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Staff.Commands;

public class UpdateEmployeeResult
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? TemporalPassword { get; set; }
}

public class UpdateEmployeeCommand : IRequest<UpdateEmployeeResult>
{
    public Guid EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PreferredLocale { get; set; }
    public Guid RoleCatalogItemId { get; set; }
    public Guid[] Skills { get; set; } = Array.Empty<Guid>();

    public bool CreateUserAccess { get; set; }
    public Guid? SystemRoleId { get; set; }
}

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, UpdateEmployeeResult>
{
    private readonly ITenantDbContext _db;
    private readonly ISecurityDbContext _securityDb;
    private readonly ITenantResolver _tenantResolver;
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateEmployeeCommandHandler(
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

    public async Task<UpdateEmployeeResult> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var emp = await _db.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted, cancellationToken);

        if (emp == null)
            throw new ArgumentException("Employee not found");

        var roleExists = await _db.CatalogItems.AnyAsync(ci => ci.Id == request.RoleCatalogItemId, cancellationToken);
        if (!roleExists)
            throw new ArgumentException("Role catalog item not found");

        var identitySyncNeeded =
            emp.UserId != null &&
            (emp.FirstName != request.FirstName ||
             emp.LastName != request.LastName ||
             !string.Equals(emp.Email, request.Email, StringComparison.OrdinalIgnoreCase));

        ApplicationUser? createdUser = null;
        string? temporalPassword = null;

        if (identitySyncNeeded)
        {
            var user = await _userManager.FindByIdAsync(emp.UserId.Value.ToString());
            if (user == null)
                throw new InvalidOperationException(
                    $"El empleado tiene vinculado el usuario '{emp.UserId}' pero no existe en Identity.");

            if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(request.Email);
                if (existing != null && existing.Id != user.Id)
                    throw new ArgumentException($"Ya existe un usuario con el email '{request.Email}'.");

                user.UserName = request.Email;
                user.Email = request.Email;
            }

            user.FullName = $"{request.FirstName} {request.LastName}".Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new ArgumentException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        else if (request.CreateUserAccess && emp.UserId == null)
        {
            if (request.SystemRoleId is null || request.SystemRoleId == Guid.Empty)
                throw new ArgumentException("SystemRoleId es requerido cuando se crea acceso al sistema.");

            var systemRole = await _securityDb.Roles
                .FirstOrDefaultAsync(r => r.Id == request.SystemRoleId, cancellationToken);
            if (systemRole == null)
                throw new ArgumentException("Rol de sistema no encontrado.");

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

            var roleResult = await _userManager.AddToRoleAsync(createdUser, systemRole.Name!);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(createdUser);
                throw new ArgumentException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));
            }
            
            emp.UserId = createdUser.Id;
        }

        emp.FirstName = request.FirstName;
        emp.LastName = request.LastName;
        emp.Email = request.Email;
        emp.PhoneNumber = request.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(request.PreferredLocale))
        {
            emp.PreferredLocale = request.PreferredLocale.Trim();
        }
        emp.RoleCatalogItemId = request.RoleCatalogItemId;
        emp.SkillsJson = JsonSerializer.Serialize(request.Skills);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (createdUser != null)
            {
                await _userManager.DeleteAsync(createdUser);
            }
            throw;
        }

        return new UpdateEmployeeResult
        {
            Id = emp.Id,
            UserId = emp.UserId,
            TemporalPassword = temporalPassword
        };
    }

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

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
