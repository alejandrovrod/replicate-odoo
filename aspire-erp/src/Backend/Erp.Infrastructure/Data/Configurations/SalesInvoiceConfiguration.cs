using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoice");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(s => s.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(s => s.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.DueDate).HasColumnType("date").IsRequired();

        builder.Property(s => s.NetTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);
        builder.Property(s => s.TaxTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);
        builder.Property(s => s.GrandTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);
        builder.Property(s => s.OutstandingAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);
        builder.Property(s => s.PaidAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);

        // Module 17: global discount (ERPNext "apply on Net Total" default). Stored non-negative;
        // the equation GrandTotal = NetTotal - DiscountAmount + TaxTotal holds for both signs of Net.
        builder.Property(s => s.DiscountPercentage).HasColumnType("decimal(5,2)").IsRequired().HasDefaultValue(0);
        builder.Property(s => s.DiscountAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0);

        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .HasConstraintName("FK_SalesInvoice_Customer")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SourceWarehouse)
            .WithMany()
            .HasForeignKey(s => s.SourceWarehouseId)
            .HasConstraintName("FK_SalesInvoice_SourceWarehouse")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Items)
            .WithOne(i => i.SalesInvoice)
            .HasForeignKey(i => i.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Module 18: credit-note mode + self-referencing provenance (ERPNext "Is Return" /
        // "return_against"). Restrict on delete: a submitted original with credit notes cannot
        // vanish while its returns exist.
        builder.Property(s => s.IsReturn).HasColumnType("bit").IsRequired().HasDefaultValue(false);

        builder.HasOne(s => s.ReturnAgainst)
            .WithMany()
            .HasForeignKey(s => s.ReturnAgainstId)
            .HasConstraintName("FK_SalesInvoice_ReturnAgainst")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.TenantId, s.CompanyId, s.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UQ_SalesInvoice_Tenant_Company_InvoiceNo");
    }
}
