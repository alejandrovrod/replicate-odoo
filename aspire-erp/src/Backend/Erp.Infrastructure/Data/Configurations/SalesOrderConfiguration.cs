using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Sales order header (Task 5.2): plan.md §1 is authoritative, so Status is persisted as the enum
/// VALUE (INT DEFAULT 1) - not as the enum name like <c>PurchaseOrder.Status</c> - and the money
/// columns carry the plan's DEFAULT 0.0000.
/// </summary>
/// <remarks>
/// A sales order never posts to the ledger by itself (COGS is booked by the DeliveryNote, revenue
/// by the SalesInvoice), so the table is NOT temporal and NOT append-only. The Company FK plus the
/// gapless lookup index follow the PurchaseOrderConfiguration precedent even though plan.md §1's
/// DDL omits them (FLAG in the task report).
/// </remarks>
public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrder", table =>
        {
            // plan.md §1 CK_SalesOrder_Totals: the document totals can never go negative.
            table.HasCheckConstraint(
                "CK_SalesOrder_Totals",
                "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency: the submit transition and the delivery progress update are
        // read-modify-writes, so a stale write surfaces as ConcurrencyConflictException (409)
        // instead of silently overwriting a newer state.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();

        // plan.md §1: INT Status DEFAULT 1 (Draft). NO string conversion - see the enum docs.
        builder.Property(o => o.Status)
            .HasDefaultValue(SalesOrderStatus.Draft)
            .IsRequired();

        builder.Property(o => o.TransactionDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.DeliveryDate).HasColumnType("date").IsRequired();

        // plan.md §1 DDL defaults: 0.0000 for the totals, 0.00 for the percentages.
        builder.Property(o => o.NetTotal)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.TaxTotal)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.GrandTotal)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.DeliveredPercentage)
            .HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(o => o.BilledPercentage)
            .HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();

        builder.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .HasConstraintName("FK_SalesOrder_Customer")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .HasConstraintName("FK_SalesOrder_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the order, delete its lines.
        builder.HasMany(o => o.Lines)
            .WithOne(l => l.SalesOrder)
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless order-number lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        // Non-unique, exactly like IX_PurchaseOrder_Tenant_Company_Voucher: the UPDLOCK/HOLDLOCK
        // range lock inside the creation transaction is what serializes the sequence.
        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.OrderNumber })
            .HasDatabaseName("IX_SalesOrder_Tenant_Company_Order");
    }
}
