using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Multi-company account mapping for a supplier (module buying-suppliers, spec §1 gap D2). Child of
/// the <see cref="Supplier"/> aggregate with a composite unique key per (SupplierId, CompanyId).
/// </summary>
public sealed class SupplierCompanyAccountConfiguration : IEntityTypeConfiguration<SupplierCompanyAccount>
{
    public void Configure(EntityTypeBuilder<SupplierCompanyAccount> builder)
    {
        builder.ToTable("SupplierCompanyAccount");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.SupplierId).IsRequired();
        builder.Property(a => a.CompanyId).IsRequired();
        builder.Property(a => a.PayableAccountId).IsRequired();
        builder.Property(a => a.AdvanceAccountId);

        // Company scope: a mapping points at ONE company of the tenant (business FK, no navigation
        // owned by this aggregate).
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .HasConstraintName("FK_SupplierCompanyAccount_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.PayableAccountId)
            .HasConstraintName("FK_SupplierCompanyAccount_PayableAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.AdvanceAccountId)
            .HasConstraintName("FK_SupplierCompanyAccount_AdvanceAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SupplierId, a.CompanyId })
            .IsUnique()
            .HasDatabaseName("UQ_SupplierCompanyAccount_Supplier_Company");
    }
}
