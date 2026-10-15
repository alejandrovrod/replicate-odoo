using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Role &amp; Permission Manager DDL (spec §3): <c>UQ_UserRoles_Tenant_User_Role</c> backs
/// the double-assignment rule, with tenant-led read indexes on both access paths
/// (by user for login/matrix reads, by role for headcounts and the last-admin guard).
/// Replaces the convention single-column <c>RoleId</c>/<c>UserId</c> indexes
/// (Constitution Article IV.1 - TenantId leads).
/// </summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(u => u.Id);

        builder.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.TenantId, u.UserId, u.RoleId })
            .IsUnique()
            .HasDatabaseName("UQ_UserRoles_Tenant_User_Role");

        builder.HasIndex(u => new { u.TenantId, u.UserId })
            .HasDatabaseName("IX_UserRoles_Tenant_User");

        builder.HasIndex(u => new { u.TenantId, u.RoleId })
            .HasDatabaseName("IX_UserRoles_Tenant_Role");
    }
}
