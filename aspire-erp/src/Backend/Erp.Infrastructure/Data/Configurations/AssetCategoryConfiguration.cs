using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Asset category GL template (Task 10.1, plan.md §1 DDL table 1): temporal master with the
/// three required account FKs plus the nullable CWIP and disposal-variance links.
/// </summary>
public sealed class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> builder)
    {
        // Constitution Article IV.2: master entity -> system-versioned (plan.md §1 DDL).
        builder.ToTable("AssetCategory", table =>
            table.IsTemporal(t => t.UseHistoryTable("AssetCategoryHistory")));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(c => c.CategoryName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.IsNonDepreciable).HasDefaultValue(false);
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .HasConstraintName("FK_AssetCategory_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.FixedAssetAccount)
            .WithMany()
            .HasForeignKey(c => c.FixedAssetAccountId)
            .HasConstraintName("FK_AssetCategory_FixedAsset")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AccumulatedDepreciationAccount)
            .WithMany()
            .HasForeignKey(c => c.AccumulatedDepreciationAccountId)
            .HasConstraintName("FK_AssetCategory_AccumDep")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.DepreciationExpenseAccount)
            .WithMany()
            .HasForeignKey(c => c.DepreciationExpenseAccountId)
            .HasConstraintName("FK_AssetCategory_DepExpense")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CwipAccount)
            .WithMany()
            .HasForeignKey(c => c.CwipAccountId)
            .HasConstraintName("FK_AssetCategory_Cwip")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.GainOnDisposalAccount)
            .WithMany()
            .HasForeignKey(c => c.GainOnDisposalAccountId)
            .HasConstraintName("FK_AssetCategory_GainDisposal")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LossOnDisposalAccount)
            .WithMany()
            .HasForeignKey(c => c.LossOnDisposalAccountId)
            .HasConstraintName("FK_AssetCategory_LossDisposal")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1: TenantId leads every index on a tenant-scoped table.
        builder.HasIndex(c => new { c.TenantId, c.CompanyId })
            .HasDatabaseName("IX_AssetCategory_Tenant_Company");
    }
}
