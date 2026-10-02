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

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(o => o.VoucherNo).HasMaxLength(100).IsRequired();
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
        builder.HasMany(o => o.Lines)
            .WithOne(l => l.PurchaseOrder)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless voucher lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        builder.HasIndex(o => new { o.TenantId, o.CompanyId, o.VoucherNo })
            .HasDatabaseName("IX_PurchaseOrder_Tenant_Company_Voucher");
    }
}
