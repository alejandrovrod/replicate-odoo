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
        builder.Property(s => s.CurrencyId);
        builder.Property(s => s.PaymentTermsDays).IsRequired().HasDefaultValue(30);
        builder.Property(s => s.OutstandingAmount).HasPrecision(18, 4).IsRequired().HasDefaultValue(0.0000m);
        builder.Property(s => s.IsActive).HasDefaultValue(true);

        // Classification & commercial terms (spec §1 gaps D1/D3): nullable FKs so the migration is
        // non-destructive for existing rows.
        builder.Property(s => s.SupplierGroupId);
        builder.Property(s => s.PaymentTermsTemplateId);
        builder.Property(s => s.DefaultPriceListId);

        // Governance (spec §1 gap D4): IsFrozen/OnHold default 0, HoldType INT (enum value),
        // ReleaseDate DATE NULL - the spec DDL verbatim.
        builder.Property(s => s.IsFrozen).HasDefaultValue(false);
        builder.Property(s => s.OnHold).HasDefaultValue(false);
        builder.Property(s => s.HoldType).IsRequired().HasDefaultValue(SupplierHoldType.None);
        builder.Property(s => s.ReleaseDate).HasColumnType("date");

        // Extensibility Fase 1 (gap D1): free-form JSON object string, NULL when none.
        builder.Property(s => s.CustomData).HasColumnType("NVARCHAR(MAX)");

        // Optimistic concurrency (specs ST-06/BY-06): store-generated rowversion token.
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(s => s.DefaultPayableAccount)
            .WithMany()
            .HasForeignKey(s => s.DefaultPayableAccountId)
            .HasConstraintName("FK_Supplier_Account")
            .OnDelete(DeleteBehavior.Restrict);

        // RM-09: billing currency link to the global Currency catalog (nullable for legacy rows).
        builder.HasOne(s => s.Currency)
            .WithMany()
            .HasForeignKey(s => s.CurrencyId)
            .HasConstraintName("FK_Supplier_Currency")
            .OnDelete(DeleteBehavior.Restrict);

        // Classification (spec §1 gap D1): nullable FK to the tenant's SupplierGroup catalog.
        builder.HasOne(s => s.SupplierGroup)
            .WithMany()
            .HasForeignKey(s => s.SupplierGroupId)
            .HasConstraintName("FK_Supplier_SupplierGroup")
            .OnDelete(DeleteBehavior.Restrict);

        // Multi-company accounts (spec §1 gap D2): child rows cascade with their supplier.
        builder.HasMany(s => s.Accounts)
            .WithOne(a => a.Supplier)
            .HasForeignKey(a => a.SupplierId)
            .HasConstraintName("FK_SupplierCompanyAccount_Supplier")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.TenantId, s.Code })
            .IsUnique()
            .HasDatabaseName("IX_Supplier_Tenant_Code");

        // spec DDL IX_Suppliers_Tenant_Group: TenantId leads per Constitution IV.1.
        builder.HasIndex(s => new { s.TenantId, s.SupplierGroupId })
            .HasDatabaseName("IX_Supplier_Tenant_Group");
    }
}
