using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Sales order line (plan.md §1 SalesOrderItem): positive quantity, a rate that MAY be zero (a
/// free line) and the two running fulfillment counters the DeliveryNote increments.
/// </summary>
public sealed class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItem", table =>
        {
            table.HasCheckConstraint("CK_SalesOrderItem_Quantity", "[Quantity] > 0.0000");
            table.HasCheckConstraint("CK_SalesOrderItem_Rate", "[Rate] >= 0.0000");
            table.HasCheckConstraint("CK_SalesOrderItem_Amount", "[Amount] >= 0.0000");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // plan.md §1: every money/quantity column is decimal(18,4) - NOTE the selling Rate is 18,4,
        // unlike the buying lines' 18,6 (plan §1 beats the PurchaseOrderLine precedent - FLAG).
        builder.Property(l => l.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Rate).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Amount).HasColumnType("decimal(18,4)").IsRequired();

        // plan.md §1 DDL defaults: both counters start at zero (Task 5.3 moves BilledQuantity).
        builder.Property(l => l.DeliveredQuantity)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(l => l.BilledQuantity)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();

        builder.HasOne(l => l.SalesOrder)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.SalesOrderId)
            .HasConstraintName("FK_SalesOrderItem_Header")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_SalesOrderItem_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
