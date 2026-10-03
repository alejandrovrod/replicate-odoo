using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Item (SKU) table: unique (TenantId, Code) for DoD 3.1, valuation method stored as its NAME,
/// and FKs to the base UOM plus the optional income/expense accounts.
/// </summary>
public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Item");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (specs ST-06/BY-06): store-generated rowversion token.
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.Property(i => i.Code).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(150).IsRequired();

        // Persist the enum NAME ('Fifo', ...) - same precedent as Account.RootType (plan §7.3).
        builder.Property(i => i.ValuationMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.IsActive).HasDefaultValue(true);

        builder.HasOne(i => i.BaseUOM)
            .WithMany()
            .HasForeignKey(i => i.BaseUOMId)
            .HasConstraintName("FK_Item_UOM")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.IncomeAccount)
            .WithMany()
            .HasForeignKey(i => i.IncomeAccountId)
            .HasConstraintName("FK_Item_IncomeAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ExpenseAccount)
            .WithMany()
            .HasForeignKey(i => i.ExpenseAccountId)
            .HasConstraintName("FK_Item_ExpenseAccount")
            .OnDelete(DeleteBehavior.Restrict);

        // Task 3.1 DoD: unique SKU PER TENANT (items are tenant-wide, not company-scoped).
        builder.HasIndex(i => new { i.TenantId, i.Code })
            .IsUnique()
            .HasDatabaseName("IX_Item_Tenant_Code");
    }
}
