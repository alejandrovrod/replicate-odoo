using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Interfaces;

public interface ISecurityDbContext
{
    DbSet<ApplicationRole> Roles { get; set; }
    DbSet<ApplicationUser> Users { get; set; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; set; }
    DbSet<IdentityUserToken<Guid>> UserTokens { get; set; }
    DbSet<RolePermission> RolePermissions { get; set; }
    DbSet<Permission> Permissions { get; set; }
    DbSet<RefreshToken> RefreshTokens { get; set; }
    DbSet<PermissionAssignmentAudit> PermissionAssignmentAudits { get; set; }
    DbSet<UserInvitation> UserInvitations { get; set; }
    DbSet<AuditLog> AuditLogs { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
