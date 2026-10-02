using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Purchase invoice header (Task 4.3): posted on creation (VoucherNo NOT NULL inside the posting
/// transaction) with a non-negative TaxAmount. The unique (TenantId, PurchaseReceiptId) index is
/// the HARD backstop of the ONE-invoice-per-receipt three-way-match rule.
/// </summary>
public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoice", table =>
            table.HasCheckConstraint("CK_PurchaseInvoice_TaxAmount_NonNegative", "[TaxAmount] >= 0"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(i => i.TaxAmount).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(i => i.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(i => i.PurchaseReceipt)
            .WithMany()
            .HasForeignKey(i => i.PurchaseReceiptId)
            .HasConstraintName("FK_PurchaseInvoice_PurchaseReceipt")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .HasConstraintName("FK_PurchaseInvoice_Company")
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are part of the aggregate: delete the invoice, delete its lines.
        builder.HasMany(i => i.Lines)
            .WithOne(l => l.PurchaseInvoice)
            .HasForeignKey(l => l.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Gapless voucher lookup (Constitution III.4) - TenantId leads per Constitution IV.1.
        builder.HasIndex(i => new { i.TenantId, i.CompanyId, i.VoucherNo })
            .HasDatabaseName("IX_PurchaseInvoice_Tenant_Company_Voucher");

        // ONE invoice per receipt (Task 4.3): TenantId leads the unique index (Constitution IV.1).
        builder.HasIndex(i => new { i.TenantId, i.PurchaseReceiptId })
            .IsUnique()
            .HasDatabaseName("IX_PurchaseInvoice_Tenant_Receipt_Unique");
    }
}
