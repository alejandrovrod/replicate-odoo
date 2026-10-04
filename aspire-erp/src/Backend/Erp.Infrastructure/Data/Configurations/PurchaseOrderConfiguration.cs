using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Purchase order header (Task 4.1): the FIRST document with a draft lifecycle, so Status is
/// persisted (enum NAME, nvarchar(20)) and VoucherNo is assigned at creation inside the
/// transaction. A PO never posts to the ledger, so the table is NOT temporal and NOT append-only.
/// </summary>
public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrder");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec BY-06): store-generated rowversion token - this is what
        // makes concurrent Draft -> Submitted / Submitted -> PartiallyReceived transitions fail loudly
        // (DbUpdateConcurrencyException -> ConcurrencyConflictException -> 409) instead of
        // letting a stale status overwrite a newer one.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .HasDefaultValue(PurchaseOrderStatus.Draft)
            .IsRequired();

        builder.Property(o => o.TransactionDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.ScheduleDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
        
        builder.Property(o => o.NetTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0.0000m);
        builder.Property(o => o.TaxTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0.0000m);
        builder.Property(o => o.GrandTotal).HasColumnType("decimal(18,4)").HasDefaultValue(0.0000m);
        
        builder.Property(o => o.ReceivedPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0.00m);
        builder.Property(o => o.BilledPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0.00m);

        builder.ToTable(t => t.HasCheckConstraint("CK_PurchaseOrder_Totals", "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000"));

        builder.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .HasConstraintName("FK_PurchaseOrder_Supplier")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .HasConstraintName("FK_PurchaseOrder_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the order, delete its lines.
        builder.HasMany(o => o.Items)
            .WithOne(l => l.PurchaseOrder)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless voucher lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.OrderNumber })
            .HasDatabaseName("IX_PurchaseOrder_Tenant_Company_OrderNumber");
    }
}
