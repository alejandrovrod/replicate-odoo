using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Import batch header (plan.md §1 DDL) plus the BN-05 summary counts (imported/duplicates) -
/// a justified deviation from the plan DDL, which tracks only the raw row count.
/// </summary>
public sealed class BankStatementImportConfiguration : IEntityTypeConfiguration<BankStatementImport>
{
    public void Configure(EntityTypeBuilder<BankStatementImport> builder)
    {
        builder.ToTable("BankStatementImport");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(i => i.FileName).HasMaxLength(255).IsRequired();
        builder.Property(i => i.ImportDate).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(i => i.TotalTransactionsCount).HasDefaultValue(0);
        builder.Property(i => i.ImportedCount).HasDefaultValue(0);
        builder.Property(i => i.DuplicateCount).HasDefaultValue(0);

        builder.Property(i => i.ImportStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(BankImportStatus.Processed)
            .IsRequired();

        builder.HasOne(i => i.BankAccount)
            .WithMany()
            .HasForeignKey(i => i.BankAccountId)
            .HasConstraintName("FK_BankStatementImport_BankAccount")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .HasConstraintName("FK_BankStatementImport_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.BankAccountId, i.ImportDate })
            .HasDatabaseName("IX_BankStatementImport_Tenant_Account_Date");
    }
}
