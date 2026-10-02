using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Purchase receipt header (Task 4.2): posted on creation (no draft state, same decision as
/// StockEntry - VoucherNo NOT NULL assigned inside the posting transaction). Optional link to
/// the fulfilled order advances its workflow status.
/// </summary>
public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipt");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(r => r.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .HasConstraintName("FK_PurchaseReceipt_PurchaseOrder")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .HasConstraintName("FK_PurchaseReceipt_Warehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .HasConstraintName("FK_PurchaseReceipt_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the receipt, delete its lines.
        builder.HasMany(r => r.Lines)
            .WithOne(l => l.PurchaseReceipt)
            .HasForeignKey(l => l.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless voucher lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        builder.HasIndex(r => new { r.TenantId, r.CompanyId, r.VoucherNo })
            .HasDatabaseName("IX_PurchaseReceipt_Tenant_Company_Voucher");
    }
}
