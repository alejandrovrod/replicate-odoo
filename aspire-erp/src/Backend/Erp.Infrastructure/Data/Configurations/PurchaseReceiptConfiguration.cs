using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipt");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .HasDefaultValue(PurchaseReceiptStatus.Draft)
            .IsRequired();

        builder.Property(r => r.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(r => r.VoucherNo).HasMaxLength(50).IsRequired();
        builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0.0000m);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.ToTable(t => t.HasCheckConstraint("CK_PurchaseReceipt_TotalAmount", "[TotalAmount] >= 0.0000"));

        builder.HasOne(r => r.Supplier)
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .HasConstraintName("FK_PurchaseReceipt_Supplier")
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasMany(r => r.Lines)
            .WithOne(l => l.PurchaseReceipt)
            .HasForeignKey(l => l.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.CompanyId, r.VoucherNo })
            .HasDatabaseName("IX_PurchaseReceipt_Tenant_Company_VoucherNo");
    }
}
