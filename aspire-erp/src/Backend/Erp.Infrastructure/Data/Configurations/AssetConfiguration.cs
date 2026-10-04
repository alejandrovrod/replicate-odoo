using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Fixed asset master (Task 10.2, plan.md §1 DDL table 2): temporal header with the value and
/// period CHECKs, the RowVersion optimistic token (spec AS-06) and the tenant-led category
/// lookup index. Method and Status persist as their enum NAMEs (NVARCHAR per the plan DDL).
/// </summary>
public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        // Constitution Article IV.2: master entity -> system-versioned (plan.md §1 DDL).
        builder.ToTable("Asset", table =>
        {
            table.IsTemporal(t => t.UseHistoryTable("AssetHistory"));
            table.HasCheckConstraint(
                "CK_Asset_Values",
                "[GrossPurchaseAmount] > 0.0000 AND [SalvageValue] >= 0.0000 AND [AccumulatedDepreciation] >= 0.0000");
            table.HasCheckConstraint(
                "CK_Asset_Periods",
                "[TotalNumberOfDepreciations] > 0 AND [FrequencyInMonths] > 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec AS-06): store-generated rowversion token.
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.Property(a => a.AssetCode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AssetName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.PurchaseDate).HasColumnType("date").IsRequired();
        builder.Property(a => a.AvailableForUseDate).HasColumnType("date").IsRequired();
        builder.Property(a => a.GrossPurchaseAmount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(a => a.SalvageValue).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(a => a.AccumulatedDepreciation).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);

        // Persist the enum NAMEs ('StraightLine', 'Draft', ...) - the plan DDL declares these
        // columns NVARCHAR(30), so string conversion matches the plan (unlike WorkOrder's INT).
        builder.Property(a => a.DepreciationMethod)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(DepreciationMethod.StraightLine);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(AssetStatus.Draft);

        builder.Property(a => a.TotalNumberOfDepreciations).IsRequired();
        builder.Property(a => a.FrequencyInMonths).IsRequired().HasDefaultValue(1);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Task 10.5 disposal stamp: NULL until the asset is Sold/Scrapped (the Block C migration
        // adds the physical column; code-first mapping stays additive so no existing row changes).
        builder.Property(a => a.DisposalDate).HasColumnType("date").IsRequired(false);

        // NetBookValue is a pure computed getter (Gross − Accumulated) - never mapped, so the
        // store carries no redundant column that could drift from its inputs.
        builder.Ignore(a => a.NetBookValue);

        builder.HasOne(a => a.Item)
            .WithMany()
            .HasForeignKey(a => a.ItemId)
            .HasConstraintName("FK_Asset_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssetCategory)
            .WithMany()
            .HasForeignKey(a => a.AssetCategoryId)
            .HasConstraintName("FK_Asset_Category")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .HasConstraintName("FK_Asset_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Plan DDL lookup - TenantId leads per Constitution IV.1.
        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.AssetCategoryId, a.Status })
            .HasDatabaseName("IX_Asset_Tenant_Category");
    }
}
