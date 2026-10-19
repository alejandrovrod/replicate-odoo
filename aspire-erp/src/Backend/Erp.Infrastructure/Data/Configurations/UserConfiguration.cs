using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// User Management DDL (spec D3). Adds the lifecycle/audit/concurrency columns, tightens the
/// email and full-name lengths to the spec's <c>NVARCHAR(256)</c>/<c>NVARCHAR(200)</c>, and
/// creates the tenant-led unique index <c>UQ_Users_Tenant_Email</c> plus
/// <c>IX_Users_TenantId</c> (Constitution Article IV.1). The MFA/lockout columns from module
/// 15 keep their exact shapes so the migration only adds/alters what the spec requires.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PasswordHash).HasColumnType("nvarchar(max)").IsRequired();

        // User Management lifecycle (spec D3): additive, defaulted, backward compatible.
        builder.Property(u => u.IsActive).HasColumnType("bit").HasDefaultValue(true);
        builder.Property(u => u.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Optimistic concurrency: store-generated rowversion token (spec D2 USER-G3).
        builder.Property(u => u.RowVersion).IsRowVersion();

        // MFA & brute-force guard (module 15): unchanged shapes.
        builder.Property(u => u.TwoFactorEnabled).HasColumnType("bit").HasDefaultValue(false);
        builder.Property(u => u.AccessFailedCount).HasColumnType("int").HasDefaultValue(0);
        builder.Property(u => u.LockoutEnd).HasColumnType("datetimeoffset");
        builder.Property(u => u.AuthenticatorKey).HasColumnType("nvarchar(max)");

        // Tenant-scoped email uniqueness (USER-G2): the same address in another tenant is legal.
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasDatabaseName("UQ_Users_Tenant_Email");

        // Constitution Article IV.1: TenantId leads every tenant-scoped index.
        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("IX_Users_TenantId");

        builder.HasMany(u => u.RecoveryCodes)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
