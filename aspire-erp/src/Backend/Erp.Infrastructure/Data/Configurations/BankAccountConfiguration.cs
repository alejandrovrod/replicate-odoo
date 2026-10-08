using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §1: system-versioned bank profile linking an account number to a GL account.</summary>
public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccount", table =>
            table.IsTemporal(t => t.UseHistoryTable("BankAccountHistory")));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(a => a.AccountName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.BankName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(a => a.CurrencyId);

        // Optimistic concurrency: store-generated rowversion token (coexists with the
        // temporal history table - rowversion is a regular column, the period stays datetime2).
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.Property(a => a.LastReconciledBalance)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(a => a.LastReconciledDate).HasColumnType("date");
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        // plan.md §1 FK_BankAccount_Company / FK_BankAccount_GLAccount: plain FK semantics =
        // SQL Server default NO ACTION (EF's convention would silently become ON DELETE CASCADE).
        builder.HasOne(a => a.Company)
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .HasConstraintName("FK_BankAccount_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.GLAccount)
            .WithMany()
            .HasForeignKey(a => a.GLAccountId)
            .HasConstraintName("FK_BankAccount_GLAccount")
            .OnDelete(DeleteBehavior.Restrict);

        // RM-09: account currency link to the global Currency catalog (nullable for legacy rows).
        builder.HasOne(a => a.Currency)
            .WithMany()
            .HasForeignKey(a => a.CurrencyId)
            .HasConstraintName("FK_BankAccount_Currency")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.CompanyId, a.AccountNumber })
            .HasDatabaseName("IX_BankAccount_Tenant_Company_Number");
    }
}
