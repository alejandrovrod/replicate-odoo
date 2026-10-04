using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Supplier master table (Task 4.1). Deliberately NOT system-versioned: Constitution Article
/// IV.2 lists Account and Company only, and the Phase 3 decision documented in seed-dev-stock.sql
/// extends that reading to the transactional masters (Item, Warehouse, UOM) - Supplier follows the
/// same precedent. Task 4.1: codes are unique per TENANT and TenantId leads the composite index
/// (Constitution IV.1).
/// </summary>
public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Supplier");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.TaxId).HasMaxLength(50).IsRequired().HasDefaultValue("");
        builder.Property(s => s.BillingCurrency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(s => s.PaymentTermsDays).IsRequired().HasDefaultValue(30);
        builder.Property(s => s.OutstandingAmount).HasPrecision(18, 4).IsRequired().HasDefaultValue(0.0000m);
        builder.Property(s => s.IsActive).HasDefaultValue(true);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.DefaultPayableAccountId)
            .HasConstraintName("FK_Supplier_Account")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.Code })
            .IsUnique()
            .HasDatabaseName("IX_Supplier_Tenant_Code");
    }
}
