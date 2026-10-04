using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>One allocation slice of a payment voucher against a sales invoice.</summary>
public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocation");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AllocatedAmount).HasColumnType("decimal(18,4)").IsRequired();

        builder.HasOne(a => a.SalesInvoice)
            .WithMany()
            .HasForeignKey(a => a.SalesInvoiceId)
            .HasConstraintName("FK_PaymentAllocation_SalesInvoice")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.PaymentEntryId })
            .HasDatabaseName("IX_PaymentAllocation_Tenant_Payment");
    }
}
