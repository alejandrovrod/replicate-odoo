using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>Purchase order line: positive quantity (decimal(18,4)) and committed rate > 0 (decimal(18,6)).</summary>
public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLine", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderLine_Qty_Positive", "[Qty] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderLine_Rate_Positive", "[Rate] > 0");
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
            .HasConstraintName("FK_PurchaseOrderLine_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
