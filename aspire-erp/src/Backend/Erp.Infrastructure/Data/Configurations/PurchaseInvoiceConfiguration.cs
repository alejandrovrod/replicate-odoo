using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Purchase invoice header (Task 4.3): posted on creation (VoucherNo NOT NULL inside the posting
/// transaction) with a non-negative TaxAmount. Several invoices may reference ONE receipt; the
/// three-way-match ceiling is the CUMULATIVE billed quantity per receipt line, enforced in code by
/// <see cref="Erp.Domain.Services.ThreeWayMatchValidator"/> before any row is written (Task 4.4).
/// </summary>
public sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoice", table =>
            table.HasCheckConstraint("CK_PurchaseInvoice_Totals", "[NetTotal] >= 0.0000 AND [TaxTotal] >= 0.0000 AND [GrandTotal] >= 0.0000"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(i => i.DueDate).HasColumnType("date").IsRequired();
        builder.Property(i => i.NetTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(i => i.TaxTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(i => i.WithholdingTaxTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(i => i.GrandTotal).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(i => i.OutstandingAmount).HasColumnType("decimal(18,4)").IsRequired().HasDefaultValue(0.0000m);
        builder.Property(i => i.VoucherNo).HasMaxLength(100).IsRequired();
        builder.Property(i => i.BillNumber).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        
        builder.Property(i => i.RowVersion)
            .IsRowVersion()
            .IsRequired();

        builder.HasOne(i => i.Supplier)
            .WithMany()
            .HasForeignKey(i => i.SupplierId)
            .HasConstraintName("FK_PurchaseInvoice_Supplier")
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
    }
}
