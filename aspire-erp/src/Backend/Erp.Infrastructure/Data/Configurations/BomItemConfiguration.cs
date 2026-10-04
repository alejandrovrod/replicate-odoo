using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>BOM component line (plan.md §1 DDL table 3): quantity/rate/amount check constraints.</summary>
public sealed class BomItemConfiguration : IEntityTypeConfiguration<BomItem>
{
    public void Configure(EntityTypeBuilder<BomItem> builder)
    {
        builder.ToTable("BomItem", table =>
        {
            table.HasCheckConstraint("CK_BOMItem_Quantity", "[Quantity] > 0.0000");
            table.HasCheckConstraint("CK_BOMItem_Rate", "[ValuationRate] >= 0.0000");
            table.HasCheckConstraint("CK_BOMItem_Amount", "[Amount] >= 0.0000");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.ValuationRate).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(i => i.Amount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(i => i.ScrapPercentage).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0m);

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .HasConstraintName("FK_BOMItem_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
