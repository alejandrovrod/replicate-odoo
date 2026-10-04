using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Work order header (Task 9.3, plan.md §1 DDL table 4): the production authorization with its
/// gapless WO number, planned/actual dates and three-warehouse routing. Status is persisted as
/// INT mirroring the PurchaseOrder precedent; RowVersion is the MF-06 optimistic token.
/// </summary>
public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrder", table =>
        {
            table.HasCheckConstraint(
                "CK_WorkOrder_Quantities",
                "[QuantityToProduce] > 0.0000 AND [ProducedQuantity] >= 0.0000");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec MF-06): store-generated rowversion token - concurrent
        // Draft -> Submitted / Submitted -> InProcess / InProcess -> Completed transitions fail
        // loudly (DbUpdateConcurrencyException -> ConcurrencyConflictException -> 409) instead of
        // letting a stale status overwrite a newer one.
        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .HasDefaultValue(WorkOrderStatus.Draft)
            .IsRequired();

        builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(o => o.QuantityToProduce).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(o => o.ProducedQuantity).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(o => o.PlannedStartDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.PlannedEndDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.ActualStartDate).HasColumnType("date");
        builder.Property(o => o.ActualEndDate).HasColumnType("date");
        builder.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(o => o.ProductionItem)
            .WithMany()
            .HasForeignKey(o => o.ProductionItemId)
            .HasConstraintName("FK_WorkOrder_Item")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Bom)
            .WithMany()
            .HasForeignKey(o => o.BomId)
            .HasConstraintName("FK_WorkOrder_BOM")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.SourceWarehouse)
            .WithMany()
            .HasForeignKey(o => o.SourceWarehouseId)
            .HasConstraintName("FK_WorkOrder_SourceWarehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.WipWarehouse)
            .WithMany()
            .HasForeignKey(o => o.WipWarehouseId)
            .HasConstraintName("FK_WorkOrder_WipWarehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.TargetWarehouse)
            .WithMany()
            .HasForeignKey(o => o.TargetWarehouseId)
            .HasConstraintName("FK_WorkOrder_TargetWarehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .HasConstraintName("FK_WorkOrder_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Plan DDL status lookup - TenantId leads per Constitution IV.1.
        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.Status })
            .HasDatabaseName("IX_WorkOrder_Tenant_Status");

        // Gapless voucher lookup (Constitution III.4), mirroring the purchase-order index.
        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.OrderNumber })
            .HasDatabaseName("IX_WorkOrder_Tenant_Company_OrderNumber");
    }
}
