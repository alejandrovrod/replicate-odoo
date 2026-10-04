using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItem", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderItem_Quantity", "[Quantity] > 0.0000");
            table.HasCheckConstraint("CK_PurchaseOrderItem_Rate", "[Rate] >= 0.0000");
            table.HasCheckConstraint("CK_PurchaseOrderItem_Amount", "[Amount] >= 0.0000");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.ReceivedQuantity).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(l => l.BilledQuantity).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(l => l.Rate).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Amount).HasColumnType("decimal(18,4)").IsRequired();
        
        builder.Property(l => l.LineNumber).IsRequired();

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_PurchaseOrderItem_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
