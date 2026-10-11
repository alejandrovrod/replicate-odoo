using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Module 17-sales-taxes-discounts: "Taxes and Charges" rows. Money shapes mirror the
/// SalesInvoice configuration (decimal(18,4)); rate is a percentage with 2 decimals.
/// </summary>
public sealed class SalesInvoiceTaxConfiguration : IEntityTypeConfiguration<SalesInvoiceTax>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceTax> builder)
    {
        builder.ToTable("SalesInvoiceTax");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Rate).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0);
        builder.Property(t => t.TaxAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);

        builder.HasOne(t => t.SalesInvoice)
            .WithMany(s => s.Taxes)
            .HasForeignKey(t => t.SalesInvoiceId)
            .HasConstraintName("FK_SalesInvoiceTax_SalesInvoice")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("FK_SalesInvoiceTax_TaxAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.SalesInvoiceId).HasDatabaseName("IX_SalesInvoiceTax_SalesInvoiceId");
    }
}
