using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Purchase invoice line: billed quantity (decimal(18,4)) must EQUAL the referenced receipt line's
/// quantity (enforced by the posting engine, full three-way match) and the billed rate
/// (decimal(18,6)) may be zero but never negative.
/// </summary>
public sealed class PurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> builder)
    {
        builder.ToTable("PurchaseInvoiceLine", table =>
        {
            table.HasCheckConstraint("CK_PurchaseInvoiceLine_Qty_Positive", "[Qty] > 0");
            table.HasCheckConstraint("CK_PurchaseInvoiceLine_Rate_NonNegative", "[Rate] >= 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.Qty).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(l => l.Rate).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(l => l.LineNumber).IsRequired();

        builder.HasOne(l => l.PurchaseReceiptLine)
            .WithMany()
            .HasForeignKey(l => l.PurchaseReceiptLineId)
            .HasConstraintName("FK_PurchaseInvoiceLine_PurchaseReceiptLine")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .HasConstraintName("FK_PurchaseInvoiceLine_Item")
            .OnDelete(DeleteBehavior.Restrict);

        // Three-way match anchor lookup: which invoice lines settle a given receipt line.
        // (Line entities carry no TenantId - same decision as StockEntryItem - so the FK leads.)
        builder.HasIndex(l => l.PurchaseReceiptLineId)
            .HasDatabaseName("IX_PurchaseInvoiceLine_PurchaseReceiptLineId");
    }
}
