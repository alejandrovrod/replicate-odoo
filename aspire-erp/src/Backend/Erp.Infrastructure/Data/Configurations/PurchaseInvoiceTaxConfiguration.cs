using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Tax-engine Fase 2: "Taxes and Charges" rows of a purchase invoice. Money shapes mirror
/// <see cref="SalesInvoiceTaxConfiguration"/> (decimal(18,4)); rate is a percentage.
/// </summary>
public sealed class PurchaseInvoiceTaxConfiguration : IEntityTypeConfiguration<PurchaseInvoiceTax>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceTax> builder)
    {
        builder.ToTable("PurchaseInvoiceTax");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.Rate).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0);
        builder.Property(t => t.FixedAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(t => t.BaseAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0m);
        builder.Property(t => t.TaxAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);

        builder.Property(t => t.ChargeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(TaxChargeType.Add);
        builder.Property(t => t.CalculationBasis)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(TaxCalculationBasis.OnNetTotal);

        builder.HasOne(t => t.PurchaseInvoice)
            .WithMany(i => i.Taxes)
            .HasForeignKey(t => t.PurchaseInvoiceId)
            .HasConstraintName("FK_PurchaseInvoiceTax_PurchaseInvoice")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("FK_PurchaseInvoiceTax_TaxAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.PurchaseInvoiceId).HasDatabaseName("IX_PurchaseInvoiceTax_PurchaseInvoiceId");
    }
}
