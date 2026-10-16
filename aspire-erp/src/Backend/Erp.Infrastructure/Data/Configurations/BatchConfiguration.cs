using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Batch-and-serial Fase 3: batch master. The explicit configuration reproduces the EXACT
/// convention-mapped DDL the table was created with (BatchId nvarchar(max) required, dates
/// as date, Cascade to Item with NO constraint name) so adopting it produces zero diff on
/// existing columns - only the Fase 3 additions arrive: <c>IsDisabled</c> and the unique
/// (TenantId, ItemId, BatchId) backstop of the duplicate-batch rule.
/// </summary>
public sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(b => b.BatchId).IsRequired();
        builder.Property(b => b.ManufacturingDate).HasColumnType("date");
        builder.Property(b => b.ExpiryDate).HasColumnType("date");
        builder.Property(b => b.IsDisabled).IsRequired().HasDefaultValue(false);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(b => b.Item)
            .WithMany()
            .HasForeignKey(b => b.ItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // Constitution Article IV.1: TenantId leads the composite index, UNIQUE per
        // (tenant, item, batch number) - the database is the authority against raced
        // duplicates (the service's GetByItemAndNumberAsync pre-check keeps the 409 UX).
        builder.HasIndex(b => new { b.TenantId, b.ItemId, b.BatchId })
            .IsUnique()
            .HasDatabaseName("UQ_Batch_Tenant_Item_Number");
    }
}

/// <summary>
/// Batch-and-serial Fase 3: serial master. Same DDL-reproduction note as
/// <see cref="BatchConfiguration"/> (SerialNumber/Status nvarchar(max) required, Cascade to
/// Item, optional Batch/Warehouse links): only the Fase 3 additions arrive -
/// <c>StockEntryItemId</c> (last stock-entry line that moved the serial) and the unique
/// (TenantId, SerialNumber) backstop of the never-reuse rule.
/// </summary>
public sealed class SerialNoConfiguration : IEntityTypeConfiguration<SerialNo>
{
    public void Configure(EntityTypeBuilder<SerialNo> builder)
    {
        builder.ToTable("SerialNos");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.SerialNumber).IsRequired();
        builder.Property(s => s.Status).IsRequired().HasDefaultValue(SerialNoStatus.Active);
        builder.Property(s => s.PurchaseDate).HasColumnType("date");
        builder.Property(s => s.DeliveryDate).HasColumnType("date");
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(s => s.Batch)
            .WithMany()
            .HasForeignKey(s => s.BatchId)
            .HasConstraintName("FK_SerialNos_Batches_BatchId");

        builder.HasOne(s => s.CurrentWarehouse)
            .WithMany()
            .HasForeignKey(s => s.CurrentWarehouseId)
            .HasConstraintName("FK_SerialNos_Warehouse_CurrentWarehouseId");

        builder.HasIndex(s => new { s.TenantId, s.SerialNumber })
            .IsUnique()
            .HasDatabaseName("UQ_SerialNo_Tenant_Number");
    }
}
