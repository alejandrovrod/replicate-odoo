using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Delivery note line (plan.md §1.6): positive quantity only - there is no rate here, the stock
/// value comes from the FIFO cost layers, not from the order rate.
/// </summary>
public sealed class DeliveryNoteLineConfiguration : IEntityTypeConfiguration<DeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteLine> builder)
    {
        builder.ToTable("DeliveryNoteLine", table =>
        {
            table.HasCheckConstraint("CK_DeliveryNoteLine_Qty", "[Qty] > 0.0000");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Qty).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(l => l.DeliveryNote)
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.DeliveryNoteId)
            .HasConstraintName("FK_DeliveryNoteLine_Header")
            .OnDelete(DeleteBehavior.Cascade);

        // The order line the SL-04 non-overdelivery guard checks against.
        builder.HasOne(l => l.SalesOrderItem)
            .WithMany()
            .HasForeignKey(l => l.SalesOrderItemId)
            .HasConstraintName("FK_DeliveryNoteLine_OrderLine")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_DeliveryNoteLine_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
