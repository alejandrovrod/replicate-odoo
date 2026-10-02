using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Stock voucher line: positive quantity (decimal(18,4)) and an optional 6-decimal rate.</summary>
public sealed class StockEntryItemConfiguration : IEntityTypeConfiguration<StockEntryItem>
{
    public void Configure(EntityTypeBuilder<StockEntryItem> builder)
    {
        builder.ToTable("StockEntryItem", table =>
            table.HasCheckConstraint("CK_StockEntryItem_Qty_Positive", "[Qty] > 0"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Qty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Rate).HasColumnType("decimal(18,6)");
        builder.Property(l => l.LineNumber).IsRequired();

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_StockEntryItem_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
