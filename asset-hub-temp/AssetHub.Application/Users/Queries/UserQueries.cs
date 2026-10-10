using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Users.Queries;

/// <summary>
/// Lista usuarios del tenant con búsqueda y paginación.
/// </summary>
public class GetUsersQuery : IRequest<UsersResult>
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UsersResult
{
    public List<UserDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserRoleDto> Roles { get; set; } = new();
}

public class UserRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, UsersResult>
{
    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public GetUsersQueryHandler(ISecurityDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<UsersResult> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId);

        var search = (request.Search ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLower();
            query = query.Where(u =>
                (u.FullName ?? string.Empty).ToLower().Contains(term) ||
                (u.Email ?? string.Empty).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        // Roles por usuario (una sola query)
        var rolePairs = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleId = r.Id, Name = r.Name ?? string.Empty }
        ).ToListAsync(cancellationToken);

        var rolesByUser = rolePairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(p => new UserRoleDto { Id = p.RoleId, Name = p.Name }).ToList());

        return new UsersResult
        {
            Items = users.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Roles = rolesByUser.TryGetValue(u.Id, out var roles) ? roles : new List<UserRoleDto>()
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

/// <summary>
/// Detalle de un usuario del tenant.
/// </summary>
public class GetUserByIdQuery : IRequest<UserDto?>
{
    public Guid UserId { get; set; }
}

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public GetUserByIdQueryHandler(ISecurityDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == tenantId, cancellationToken);
        if (user == null) return null;

        var roles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ur.UserId == user.Id
            select new UserRoleDto { Id = r.Id, Name = r.Name ?? string.Empty }
        ).ToListAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roles
        };
    }
}

/// <summary>
/// Invitaciones del tenant (pendientes, aceptadas y canceladas).
/// </summary>
public class GetInvitationsQuery : IRequest<List<InvitationDto>>
{
    public bool OnlyPending { get; set; }
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsExpired => AcceptedAt == null && CancelledAt == null && ExpiresAt < DateTime.UtcNow;
}

public class GetInvitationsQueryHandler : IRequestHandler<GetInvitationsQuery, List<InvitationDto>>
{
    private readonly ISecurityDbContext _db;
    private readonly ITenantResolver _tenantResolver;

    public GetInvitationsQueryHandler(ISecurityDbContext db, ITenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    public async Task<List<InvitationDto>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        var query = _db.UserInvitations
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        if (request.OnlyPending)
        {
            query = query.Where(i => i.AcceptedAt == null && i.CancelledAt == null);
        }

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var roleIds = invitations.Select(i => i.RoleId).Distinct().ToList();
        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, cancellationToken);

        return invitations.Select(i => new InvitationDto
        {
            Id = i.Id,
            Email = i.Email,
            RoleId = i.RoleId,
            RoleName = roles.TryGetValue(i.RoleId, out var name) ? name : string.Empty,
            ExpiresAt = i.ExpiresAt,
            CreatedAt = i.CreatedAt,
            AcceptedAt = i.AcceptedAt,
            CancelledAt = i.CancelledAt
        }).ToList();
    }
}

/// <summary>
/// Valida un token de invitación (endpoint público para la pantalla de
/// aceptación: muestra email/rol/expiración antes de pedir contraseña).
/// </summary>
public class GetInvitationByTokenQuery : IRequest<InvitationInfoDto?>
{
    public string Token { get; set; } = string.Empty;
}

public class InvitationInfoDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; }
    public string? InvalidReason { get; set; }
}

public class GetInvitationByTokenQueryHandler : IRequestHandler<GetInvitationByTokenQuery, InvitationInfoDto?>
{
    private readonly ISecurityDbContext _db;
    private readonly IPlatformDbContext _platformDb;

    public GetInvitationByTokenQueryHandler(ISecurityDbContext db, IPlatformDbContext platformDb)
    {
        _db = db;
        _platformDb = platformDb;
    }

    public async Task<InvitationInfoDto?> Handle(GetInvitationByTokenQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return null;

        var invitation = await _db.UserInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken);
        if (invitation == null) return null;

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == invitation.RoleId, cancellationToken);

        var tenant = await _platformDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == invitation.TenantId, cancellationToken);

        var (isValid, reason) = (invitation.AcceptedAt, invitation.CancelledAt, invitation.ExpiresAt < DateTime.UtcNow) switch
        {
            (not null, _, _) => (false, "La invitación ya fue utilizada."),
            (_, not null, _) => (false, "La invitación fue cancelada."),
            (_, _, true) => (false, "La invitación expiró."),
            _ => (true, (string?)null)
        };

        return new InvitationInfoDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            RoleName = role?.Name ?? string.Empty,
            TenantName = tenant?.Name ?? string.Empty,
            ExpiresAt = invitation.ExpiresAt,
            IsValid = isValid,
            InvalidReason = reason
        };
    }
}
