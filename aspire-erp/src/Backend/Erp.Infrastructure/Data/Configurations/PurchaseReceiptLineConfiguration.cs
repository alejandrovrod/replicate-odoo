using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("PurchaseReceiptLine");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.Qty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.Rate).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.Amount).HasColumnType("decimal(18,4)").IsRequired();

        builder.ToTable(t => 
        {
            t.HasCheckConstraint("CK_PurchaseReceiptLine_Quantity", "[Qty] > 0.0000");
            t.HasCheckConstraint("CK_PurchaseReceiptLine_Rate", "[Rate] >= 0.0000");
            t.HasCheckConstraint("CK_PurchaseReceiptLine_Amount", "[Amount] >= 0.0000");
        });

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .HasConstraintName("FK_PurchaseReceiptLine_Item")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
