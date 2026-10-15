using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Role &amp; Permission Manager DDL (spec §3): <c>Name NVARCHAR(100)</c> so the tenant-led
/// unique index is keyable (SQL Server cannot index <c>nvarchar(max)</c>).
/// <c>UQ_Roles_Tenant_Name</c> backs the SEC-G2 duplicate rule and the seeder's
/// check-then-insert convergence; its leftmost <c>TenantId</c> prefix also serves the
/// tenant-scoped reads (Constitution Article IV - no redundant single-column index).
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(r => r.IsSystemDefault).HasColumnType("bit");

        builder.HasMany(r => r.Permissions)
            .WithOne(p => p.Role)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Roles_Tenant_Name");
    }
}
