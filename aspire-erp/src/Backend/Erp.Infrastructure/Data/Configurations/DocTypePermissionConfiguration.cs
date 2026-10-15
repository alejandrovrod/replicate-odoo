using Erp.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Role &amp; Permission Manager DDL (spec §3): <c>DocType NVARCHAR(100)</c> (was
/// <c>nvarchar(max)</c>, unindexable) plus the tenant-led unique backstop
/// <c>UQ_DocPerms_Tenant_Role_DocType</c>. The unique index doubles as the
/// <c>(TenantId, RoleId, DocType)</c> read index - no duplicate non-unique index.
/// Replaces the convention <c>IX_DocTypePermissions_RoleId</c> single-column index
/// (tenant must lead every index, Constitution Article IV.1).
/// </summary>
public sealed class DocTypePermissionConfiguration : IEntityTypeConfiguration<DocTypePermission>
{
    public void Configure(EntityTypeBuilder<DocTypePermission> builder)
    {
        builder.ToTable("DocTypePermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DocType).HasMaxLength(100).IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.RoleId, p.DocType })
            .IsUnique()
            .HasDatabaseName("UQ_DocPerms_Tenant_Role_DocType");
    }
}
