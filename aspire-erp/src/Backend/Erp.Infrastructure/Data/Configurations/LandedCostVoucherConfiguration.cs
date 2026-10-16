using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Landed cost voucher aggregate (Fase 4): header with Draft lifecycle, gapless number at
/// submit, optimistic RowVersion token and the three child collections.
/// </summary>
public sealed class LandedCostVoucherConfiguration : IEntityTypeConfiguration<LandedCostVoucher>
{
    public void Configure(EntityTypeBuilder<LandedCostVoucher> builder)
    {
        builder.ToTable("LandedCostVoucher");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(v => v.VoucherNo).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(v => v.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(DocumentStatus.Draft);
        builder.Property(v => v.DistributeBy)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(LandedCostDistributeBy.Amount);
        builder.Property(v => v.TotalTaxesAndCharges).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(v => v.RowVersion).IsRowVersion();
        builder.Property(v => v.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasMany(v => v.Receipts)
            .WithOne(r => r.LandedCostVoucher)
            .HasForeignKey(r => r.LandedCostVoucherId)
            .HasConstraintName("FK_LandedCostReceipt_Voucher")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Charges)
            .WithOne(c => c.LandedCostVoucher)
            .HasForeignKey(c => c.LandedCostVoucherId)
            .HasConstraintName("FK_LandedCostCharge_Voucher")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Items)
            .WithOne(i => i.LandedCostVoucher)
            .HasForeignKey(i => i.LandedCostVoucherId)
            .HasConstraintName("FK_LandedCostItem_Voucher")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.TenantId, v.CompanyId, v.VoucherNo })
            .HasDatabaseName("IX_LandedCostVoucher_Tenant_Company_Number");
    }
}

/// <summary>Landed cost receipt links, charges and apportioned lines (child tables).</summary>
public sealed class LandedCostReceiptReferenceConfiguration : IEntityTypeConfiguration<LandedCostReceiptReference>
{
    public void Configure(EntityTypeBuilder<LandedCostReceiptReference> builder)
    {
        builder.ToTable("LandedCostReceiptReference");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.HasOne(r => r.PurchaseReceipt)
            .WithMany()
            .HasForeignKey(r => r.PurchaseReceiptId)
            .HasConstraintName("FK_LandedCostReceipt_Receipt")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LandedCostChargeItemConfiguration : IEntityTypeConfiguration<LandedCostChargeItem>
{
    public void Configure(EntityTypeBuilder<LandedCostChargeItem> builder)
    {
        builder.ToTable("LandedCostChargeItem");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.Description).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Amount).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(c => c.ExpenseAccount)
            .WithMany()
            .HasForeignKey(c => c.ExpenseAccountId)
            .HasConstraintName("FK_LandedCostCharge_Account")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LandedCostApportionmentItemConfiguration : IEntityTypeConfiguration<LandedCostApportionmentItem>
{
    public void Configure(EntityTypeBuilder<LandedCostApportionmentItem> builder)
    {
        builder.ToTable("LandedCostApportionmentItem");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.Qty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.OriginalRate).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(i => i.ApplicableCharges).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(i => i.NewRate).HasColumnType("decimal(18,6)").IsRequired();

        builder.HasOne(i => i.PurchaseReceipt)
            .WithMany()
            .HasForeignKey(i => i.PurchaseReceiptId)
            .HasConstraintName("FK_LandedCostItem_Receipt")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .HasConstraintName("FK_LandedCostItem_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.LandedCostVoucherId)
            .HasDatabaseName("IX_LandedCostItem_VoucherId");
    }
}
