using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// BOM master (plan.md §1 DDL table 2): temporal history, quantity/cost check constraints and the
/// (TenantId, CompanyId, ItemId, IsActive) lookup index. Deviation: the table is
/// <c>BillOfMaterials</c> (repo convention - table names match entity names) rather than the
/// plan's <c>BOM</c>.
/// </summary>
public sealed class BillOfMaterialsConfiguration : IEntityTypeConfiguration<BillOfMaterials>
{
    public void Configure(EntityTypeBuilder<BillOfMaterials> builder)
    {
        // Master entity: system-versioned like Account/Company (Constitution Article IV.2).
        builder.ToTable("BillOfMaterials", table =>
        {
            table.IsTemporal(t => t.UseHistoryTable("BillOfMaterialsHistory"));
            table.HasCheckConstraint("CK_BOM_Quantity", "[Quantity] > 0.0000");
            table.HasCheckConstraint(
                "CK_BOM_Costs",
                "[RawMaterialCost] >= 0.0000 AND [OperatingCost] >= 0.0000 AND [TotalCost] >= 0.0000");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(b => b.BomNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Quantity).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(1m);
        builder.Property(b => b.IsActive).HasDefaultValue(true);
        builder.Property(b => b.IsDefault).HasDefaultValue(true);
        builder.Property(b => b.RawMaterialCost).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(b => b.OperatingCost).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(b => b.ScrapCost).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(b => b.TotalCost).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(b => b.Item)
            .WithMany()
            .HasForeignKey(b => b.ItemId)
            .HasConstraintName("FK_BOM_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Uom)
            .WithMany()
            .HasForeignKey(b => b.UomId)
            .HasConstraintName("FK_BOM_UOM")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines and operations are part of the aggregate: delete the BOM, delete its rows.
        builder.HasMany(b => b.Items)
            .WithOne(i => i.Bom)
            .HasForeignKey(i => i.BomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Operations)
            .WithOne(o => o.Bom)
            .HasForeignKey(o => o.BomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Plan DDL lookup index; TenantId leads per Constitution IV.1.
        builder.HasIndex(b => new { b.TenantId, b.CompanyId, b.ItemId, b.IsActive })
            .HasDatabaseName("IX_BOM_Tenant_Item");
    }
}
