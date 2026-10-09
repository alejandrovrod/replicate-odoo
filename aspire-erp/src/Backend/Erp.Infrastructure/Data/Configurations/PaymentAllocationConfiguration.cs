using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// One allocation slice of a payment voucher against exactly one invoice (spec R-12 PE-06).
/// </summary>
public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocation", table =>
        {
            // Defense in depth for PE-06: exactly one invoice reference per slice.
            table.HasCheckConstraint(
                "CK_PaymentAllocation_ExactlyOneInvoice",
                "([SalesInvoiceId] IS NOT NULL AND [PurchaseInvoiceId] IS NULL) OR ([SalesInvoiceId] IS NULL AND [PurchaseInvoiceId] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_PaymentAllocation_Amount",
                "[AllocatedAmount] > 0.0000");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AllocatedAmount).HasColumnType("decimal(18,4)").IsRequired();

        // ERPNext-parity snapshot columns (Phase 6): denormalized reference + invoice
        // figures at allocation time; NOT NULL with defaults (non-destructive migration).
        builder.Property(a => a.ReferenceDocumentType).HasMaxLength(50).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(a => a.ReferenceDocumentId);
        builder.Property(a => a.TotalAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(a => a.OutstandingAmount).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(a => a.ExchangeRate).HasColumnType("decimal(18,6)").HasDefaultValue(1m).IsRequired();

        builder.HasOne(a => a.SalesInvoice)
            .WithMany()
            .HasForeignKey(a => a.SalesInvoiceId)
            .HasConstraintName("FK_PaymentAllocation_SalesInvoice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(a => a.PurchaseInvoiceId)
            .HasConstraintName("FK_PaymentAllocation_PurchaseInvoice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.PaymentEntryId })
            .HasDatabaseName("IX_PaymentAllocation_Tenant_Payment");
    }
}
