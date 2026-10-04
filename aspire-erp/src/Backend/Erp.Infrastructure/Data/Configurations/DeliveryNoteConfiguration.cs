using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Delivery note header (Task 5.2b / Amendment A1): posted on creation - VoucherNo is assigned
/// inside the posting transaction, exactly like PurchaseReceipt/StockEntry. No money columns
/// (plan.md §1.6): the stock value comes from the FIFO cost layers at posting time.
/// </summary>
public sealed class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable("DeliveryNote");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: two concurrent deliveries of the same order race on the order
        // lines, so a stale save surfaces as ConcurrencyConflictException (409).
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.Property(d => d.VoucherNo).HasMaxLength(50).IsRequired();
        builder.Property(d => d.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(d => d.SalesOrder)
            .WithMany()
            .HasForeignKey(d => d.SalesOrderId)
            .HasConstraintName("FK_DeliveryNote_SalesOrder")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Warehouse)
            .WithMany()
            .HasForeignKey(d => d.WarehouseId)
            .HasConstraintName("FK_DeliveryNote_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .HasConstraintName("FK_DeliveryNote_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the note, delete its lines.
        builder.HasMany(d => d.Lines)
            .WithOne(l => l.DeliveryNote)
            .HasForeignKey(l => l.DeliveryNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // plan.md §1.6 UQ_DeliveryNote_Tenant_Company_VoucherNo: UNIQUE - the database is the
        // authority behind the gapless DN sequence (the UPDLOCK range lock keeps it race-free).
        builder.HasIndex(d => new { d.TenantId, d.CompanyId, d.VoucherNo })
            .IsUnique()
            .HasDatabaseName("UQ_DeliveryNote_Tenant_Company_VoucherNo");
    }
}
