using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Purchase receipt line: positive quantity (decimal(18,4)) and received rate > 0 (decimal(18,6)) - it values the incoming stock.</summary>
public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("PurchaseReceiptLine", table =>
        {
            table.HasCheckConstraint("CK_PurchaseReceiptLine_Qty_Positive", "[Qty] > 0");
            table.HasCheckConstraint("CK_PurchaseReceiptLine_Rate_Positive", "[Rate] > 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Qty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Rate).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(l => l.LineNumber).IsRequired();

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_PurchaseReceiptLine_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
