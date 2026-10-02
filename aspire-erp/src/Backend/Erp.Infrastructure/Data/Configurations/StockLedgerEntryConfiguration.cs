using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Kardex (StockLedgerEntry) table: signed QtyChange/Amount, append-only by convention, indexed
/// for FIFO reads with TenantId leading (Constitution IV.1).
/// </summary>
public sealed class StockLedgerEntryConfiguration : IEntityTypeConfiguration<StockLedgerEntry>
{
    public void Configure(EntityTypeBuilder<StockLedgerEntry> builder)
    {
        builder.ToTable("StockLedgerEntry");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.QtyChange).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(e => e.ValuationRate).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(e => e.VoucherType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .HasConstraintName("FK_StockLedgerEntry_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Warehouse)
            .WithMany()
            .HasForeignKey(e => e.WarehouseId)
            .HasConstraintName("FK_StockLedgerEntry_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StockEntry)
            .WithMany()
            .HasForeignKey(e => e.StockEntryId)
            .HasConstraintName("FK_StockLedgerEntry_StockEntry")
            .OnDelete(DeleteBehavior.Restrict);

        // FIFO layer query: (item, warehouse) rows up to a date, chronologically.
        builder.HasIndex(e => new { e.TenantId, e.ItemId, e.WarehouseId, e.PostingDate })
            .HasDatabaseName("IX_StockLedger_Tenant_Item_Warehouse_Date");
    }
}
