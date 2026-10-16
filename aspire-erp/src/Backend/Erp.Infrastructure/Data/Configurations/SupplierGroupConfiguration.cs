using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Supplier classification catalog (module buying-suppliers, spec §1 gap D1). Tenant-scoped master,
/// NOT temporal; codes are unique per TENANT and TenantId leads the composite index
/// (Constitution IV.1).
/// </summary>
public sealed class SupplierGroupConfiguration : IEntityTypeConfiguration<SupplierGroup>
{
    public void Configure(EntityTypeBuilder<SupplierGroup> builder)
    {
        builder.ToTable("SupplierGroup");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(g => g.Code).HasMaxLength(50).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(150).IsRequired();
        builder.Property(g => g.IsActive).HasDefaultValue(true);
        builder.Property(g => g.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(g => new { g.TenantId, g.Code })
            .IsUnique()
            .HasDatabaseName("UQ_SupplierGroup_Tenant_Code");
    }
}
