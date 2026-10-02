using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Unit of Measure table: unique (TenantId, Code) and a strictly positive conversion factor.</summary>
public sealed class UOMConfiguration : IEntityTypeConfiguration<UOM>
{
    public void Configure(EntityTypeBuilder<UOM> builder)
    {
        // Positive decimal constraint (Constitution IV.3 spirit): a zero/negative factor would
        // silently destroy stock valuation, so it is rejected at the database level too.
        builder.ToTable("UOM", table => table.HasCheckConstraint("CK_UOM_ToBaseFactor", "[ToBaseFactor] > 0"));

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(u => u.Code).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(50).IsRequired();
        builder.Property(u => u.ToBaseFactor).HasColumnType("decimal(18,6)").IsRequired();

        // Constitution IV.1: TenantId leads the unique index.
        builder.HasIndex(u => new { u.TenantId, u.Code })
            .IsUnique()
            .HasDatabaseName("IX_UOM_Tenant_Code");
    }
}
